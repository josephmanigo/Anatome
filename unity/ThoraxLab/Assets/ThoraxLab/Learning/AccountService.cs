using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ThoraxLab.Learning
{
    [Serializable] public sealed class SupabaseConfiguration
    {
        public string projectUrl, publishableKey;
        public int otpSeconds = 600, resendSeconds = 60;
        public bool Ready => Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host.EndsWith(".supabase.co", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(publishableKey);
    }
    [Serializable] public sealed class AuthUser { public string id, email; }
    [Serializable] public sealed class AuthSession { public string access_token, refresh_token, token_type; public long expires_at; public int expires_in; public AuthUser user; }
    [Serializable] sealed class OtpRequest { public string email; public bool create_user = true; }
    [Serializable] sealed class OtpVerification { public string email, token; public string type = "email"; }
    [Serializable] sealed class RefreshRequest { public string refresh_token; }
    [Serializable] sealed class ApiError { public string message, msg, error_description, code; }
    [Serializable] sealed class EmptyRequest { }
    [Serializable] sealed class SyncRequest { public int expected_generation; public bool orientation_completed; public AttemptRecord[] incoming_attempts; public ActivityCheckpoint[] incoming_checkpoints; }
    [Serializable] sealed class SyncReply { public int generation; public bool orientationCompleted; public AttemptRecord[] attempts; public ActivityCheckpoint[] checkpoints; public string syncedUtc; }
    [Serializable] sealed class GenerationReply { public int generation; }
    [Serializable] sealed class ProfileRequest {public SettingsProfile incoming_profile;public string delete_id;}
    [Serializable] sealed class ProfileDeleteRequest {public string delete_id;}
    [Serializable] sealed class ProfileReply {public SettingsProfile[] profiles;}

    public sealed class AccountService
    {
        public SupabaseConfiguration Configuration { get; }
        public AuthSession Session { get; private set; }
        public bool Offline { get; private set; }
        public bool Busy { get; private set; }
        public string UserId => Session?.user?.id;
        public string Email => Session?.user?.email;
        public DateTime NextCodeUtc { get; private set; }
        public DateTime CodeExpiresUtc { get; private set; }
        DateTime lastEmailAuthentication;
        readonly string localRoot;
        int accountEpoch;
        public TrustedDeviceStore Trusted { get; }
        public event Action Changed;
#if UNITY_EDITOR
        // Deterministic transport interruption for the opt-in live Editor verifier.
        // This hook and its invocation are absent from all player builds.
        public Action<UnityWebRequest> VerificationRequestStarted;
#endif

        public AccountService(string root)
        {
            localRoot=root;
            var asset = Resources.Load<TextAsset>("Learning/supabase-config");
            Configuration = asset ? JsonUtility.FromJson<SupabaseConfiguration>(asset.text) : new SupabaseConfiguration();
            Trusted = new TrustedDeviceStore(Path.Combine(root, "trusted"));
        }

        public async Task SendCode(string email)
        {
            if (DateTime.UtcNow < NextCodeUtc) throw new InvalidOperationException("Please wait before requesting another code.");
            email = (email ?? "").Trim();
            if (email.Length > 254 || email.Count(c => c == '@') != 1 || email.Any(char.IsWhiteSpace)) throw new ArgumentException("Enter a valid email address.");
            await Request("POST", "/auth/v1/otp", new OtpRequest { email = email }, false);
            NextCodeUtc = DateTime.UtcNow.AddSeconds(Configuration.resendSeconds);
            CodeExpiresUtc = DateTime.UtcNow.AddSeconds(Configuration.otpSeconds);
        }

        public async Task VerifyCode(string email, string code)
        {
            int epoch=accountEpoch;
            if (code == null || code.Length != 6 || code.Any(c => c < '0' || c > '9')) throw new ArgumentException("Enter the six-digit email code.");
            var session = JsonUtility.FromJson<AuthSession>(await Request("POST", "/auth/v1/verify", new OtpVerification { email = email.Trim(), token = code }, false));
            if(epoch!=accountEpoch)throw new InvalidOperationException("Sign-in was cancelled because the account changed.");
            AcceptSession(session); accountEpoch++;Offline = false; lastEmailAuthentication=DateTime.UtcNow; Changed?.Invoke();
        }

        void AcceptSession(AuthSession session)
        {
            if (session?.user == null || !Guid.TryParse(session.user.id, out _) || string.IsNullOrEmpty(session.access_token) || string.IsNullOrEmpty(session.refresh_token))
                throw new InvalidDataException("The sign-in response could not be verified. Please try again.");
            if (session.expires_at == 0) session.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + session.expires_in;
            Session = session;
        }

        public void UnlockOffline(string userId, string pin)
        {
            var session = Trusted.Unlock(userId, pin); AcceptSession(session); accountEpoch++;Offline = true; Changed?.Invoke();
        }
        public void TrustDevice(string pin)
        {
            if (Session == null || Offline || DateTime.UtcNow-lastEmailAuthentication>TimeSpan.FromMinutes(10)) throw new InvalidOperationException("Sign in with a fresh email code to set or change your device PIN.");
            Trusted.Enroll(Session, pin);
        }

        public async Task EnsureOnline()
        {
            if (Session == null) throw new InvalidOperationException("Sign in to synchronize progress.");
            if (Offline || DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= Session.expires_at - 90)
            {
                int epoch=accountEpoch;
                var refreshed = JsonUtility.FromJson<AuthSession>(await Request("POST", "/auth/v1/token?grant_type=refresh_token", new RefreshRequest { refresh_token = Session.refresh_token }, false));
                if(epoch!=accountEpoch)throw new InvalidOperationException("Account changed while reconnecting.");
                AcceptSession(refreshed); Trusted.Refresh(Session); Offline = false;
            }
        }

        public async Task Sync(ProgressSnapshot progress, string activeAttemptId = null)
        {
            string account = UserId;
            if (account == null || progress.userId != account) throw new InvalidOperationException("This progress belongs to a different account.");
            await EnsureOnline();
            long revision=progress.localRevision;
            var reply = JsonUtility.FromJson<SyncReply>(await Request("POST", "/rest/v1/rpc/thorax_sync_progress", new SyncRequest
            {
                expected_generation = progress.resetGeneration, orientation_completed = progress.orientationCompleted,
                incoming_attempts = progress.attempts.Where(a => !a.synced && a.status == AttemptStatus.Completed).Take(200).ToArray(), incoming_checkpoints = progress.checkpoints.OrderByDescending(c=>c.savedUtc).Take(200).ToArray()
            }, true));
            if (UserId != account) throw new InvalidOperationException("Account changed while synchronizing. Local progress remains saved.");
            if (reply == null || reply.generation < progress.resetGeneration) throw new InvalidDataException("The sync response has an invalid generation.");
            ProgressRules.ApplyReset(progress, reply.generation);
            progress.orientationCompleted |= reply.orientationCompleted;
            foreach (var attempt in reply.attempts ?? Array.Empty<AttemptRecord>())
            {
                if (attempt.userId != account || attempt.resetGeneration != progress.resetGeneration) throw new InvalidDataException("Sync returned an unrelated record.");
                attempt.synced = true;
                var existing = progress.attempts.FindIndex(a => a.id == attempt.id);
                if (existing >= 0) progress.attempts.RemoveAt(existing);
                ProgressRules.AddAttempt(progress, attempt);
            }
            // Rebuild derived rewards after server reconciliation, preventing stale local pass rewards.
            ProgressRules.RebuildRewards(progress);
            foreach (var checkpoint in reply.checkpoints ?? Array.Empty<ActivityCheckpoint>())
            {
                ProgressRules.MergeCheckpoint(progress, checkpoint, activeAttemptId);
            }
            progress.checkpoints.RemoveAll(c => progress.attempts.Any(a => a.id == c.attemptId && a.status == AttemptStatus.Completed));
            progress.lastSyncedUtc = reply.syncedUtc ?? DateTime.UtcNow.ToString("o");
            progress.pendingSync=progress.localRevision!=revision||progress.attempts.Any(a=>!a.synced&&a.status==AttemptStatus.Completed);
        }

        public async Task ResetProgress(ProgressSnapshot progress)
        {
            await EnsureOnline(); string id = UserId;
            var reply = JsonUtility.FromJson<GenerationReply>(await Request("POST", "/rest/v1/rpc/thorax_reset_progress", new EmptyRequest(), true));
            if (id != UserId || progress.userId != id) throw new InvalidOperationException("Account changed.");
            ProgressRules.ApplyReset(progress, reply.generation);
        }
        public async Task DeleteAccount()
        {
            await EnsureOnline(); string id = UserId;
            await Request("POST", "/functions/v1/thorax-delete-account", new EmptyRequest(), true);
            Trusted.Forget(id); if(UserId==id)SignOut();
            if(Guid.TryParse(id,out _)){string directory=Path.Combine(localRoot,"accounts",id);if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        public void SignOut() { Session = null; accountEpoch++;Offline = false; lastEmailAuthentication=DateTime.MinValue; Changed?.Invoke(); }
        public async Task<SettingsProfile[]> SyncProfiles(SettingsProfile profile=null,string deleteId=null)
        {
            await EnsureOnline();string id=UserId;
            var reply=JsonUtility.FromJson<ProfileReply>(await Request("POST","/rest/v1/rpc/thorax_sync_settings",ProfileSyncPayload(profile,deleteId),true));
            if(UserId!=id)throw new InvalidOperationException("Account changed while loading settings.");
            foreach(var item in reply.profiles??Array.Empty<SettingsProfile>())item.Validate();return reply.profiles??Array.Empty<SettingsProfile>();
        }
        internal static object ProfileSyncPayload(SettingsProfile profile,string deleteId)
        {
            // JsonUtility expands a null serializable class into a default object.
            // Omit the optional profile entirely for reads/deletes, or the server
            // receives an unintended new SettingsProfile on every request.
            if(profile!=null)return new ProfileRequest{incoming_profile=profile,delete_id=deleteId};
            if(!string.IsNullOrEmpty(deleteId))return new ProfileDeleteRequest{delete_id=deleteId};
            return new EmptyRequest();
        }

        public async Task<string> Request(string method, string endpoint, object payload, bool authenticated)
        {
            if (!Configuration.Ready) throw new InvalidOperationException("Email sign-in is temporarily unavailable. You can explore the lab without saving.");
            if (Busy) throw new InvalidOperationException("Please wait for the current account request to finish.");
            Busy = true;
            try
            {
                using (var request = new UnityWebRequest(Configuration.projectUrl.TrimEnd('/') + endpoint, method))
                {
                    request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 25;
                    request.SetRequestHeader("apikey", Configuration.publishableKey);
                    if (authenticated)
                    {
                        if (Session == null) throw new InvalidOperationException("Sign in first.");
                        request.SetRequestHeader("Authorization", "Bearer " + Session.access_token);
                    }
                    if (payload != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))); request.SetRequestHeader("Content-Type", "application/json"); }
                    var operation = request.SendWebRequest();
#if UNITY_EDITOR
                    VerificationRequestStarted?.Invoke(request);
#endif
                    while (!operation.isDone) await Task.Yield();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        bool signIn = endpoint == "/auth/v1/otp" || endpoint == "/auth/v1/verify";
                        if (request.responseCode == 0) throw new IOException(ConnectionMessage(signIn, Application.internetReachability));
                        if (signIn && (request.responseCode == 502 || request.responseCode == 503 || request.responseCode == 504))
                            throw new IOException("Anatome's sign-in service is temporarily unavailable. Try again shortly, or choose guest practice.");
                        if (request.responseCode == 429) throw new InvalidOperationException("Too many requests. Please wait and try again.");
                        if (request.responseCode == 401 || request.responseCode == 403) throw new InvalidOperationException("Your session or email code could not be verified. Request a new code.");
                        if (endpoint == "/auth/v1/otp" && request.responseCode >= 500 && request.responseCode <= 599) throw new InvalidOperationException("The email service could not send your code. Email sign-in is temporarily unavailable. You can choose guest practice without saving.");
                        if (endpoint.StartsWith("/rest/v1/", StringComparison.Ordinal) && (request.responseCode == 404 || request.downloadHandler.text.Contains("PGRST202"))) throw new InvalidOperationException("Progress sync is not available yet. Your progress remains saved on this device.");
                        if (request.responseCode == 400 && endpoint.Contains("verify")) throw new InvalidOperationException("That code is invalid or expired. Request a new code.");
                        throw new IOException("The account service could not complete this request. Please try again.");
                    }
                    return request.downloadHandler.text;
                }
            }
            finally { Busy = false; }
        }

        static string ConnectionMessage(bool signIn, NetworkReachability network)
        {
            if (network == NetworkReachability.NotReachable) return "This device is offline. Reconnect to Wi-Fi and try again.";
            return signIn ? "Cannot reach Anatome's sign-in service. Try again shortly, or choose guest practice."
                : "Cannot reach the account service. Your progress is saved on this device. Try again shortly.";
        }
    }
}

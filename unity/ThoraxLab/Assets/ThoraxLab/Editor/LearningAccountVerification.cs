#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Editor
{
    // Local, opt-in verification only. Never compiled into a player. OTP values
    // remain in memory and are submitted through the game's real account code.
    [InitializeOnLoad]
    public static class LearningAccountVerification
    {
        const string Pending="ThoraxLab.LiveAccountQA", Product="ThoraxLab.LiveAccountQA.Product";
        static LearningAccountVerification()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Pending,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Editor-only live account verification").AddComponent<LearningAccountVerificationRunner>();
                if(state==PlayModeStateChange.EnteredEditMode)
                {PlayerSettings.productName=SessionState.GetString(Product,LearningBrand.Name);SessionState.SetBool(Pending,false);AssetDatabase.SaveAssets();}
            };
        }
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').EndsWith("/ThoraxLabReview/Assets",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Use the isolated ThoraxLabReview project for live QA.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start outside Play.");
            var mainSettings=File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ThoraxLab/ProjectSettings/ProjectSettings.asset")));
            var originalProduct=System.Text.RegularExpressions.Regex.Match(mainSettings,@"(?m)^  productName: (.+)$").Groups[1].Value.Trim();
            SessionState.SetString(Product,string.IsNullOrEmpty(originalProduct)?PlayerSettings.productName:originalProduct);
            PlayerSettings.productName="ThoraxLabAccountVerification";
            SessionState.SetBool(Pending,true);
            EditorPrefs.SetBool("ThoraxLab.XRDeviceSimulator",true);
            EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            LearningEditorPlayMode.Request();
        }
        internal static void Close()
        {
            PlayerSettings.productName=SessionState.GetString(Product,LearningBrand.Name);
            SessionState.SetBool(Pending,false);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
    }

    public sealed class LearningAccountVerificationRunner:MonoBehaviour
    {
        const string EmailA="j.oginam2+thorax-qa-a@gmail.com",EmailB="j.oginam2+thorax-qa-b@gmail.com";
        const string Origin="http://127.0.0.1:18764";
        [Serializable] sealed class CheckResult{public string id,detail;public bool passed;}
        [Serializable] sealed class Report
        {
            public string utc,status="waiting for email codes",method="Actual Unity account service and UI handlers, isolated local stores and live Supabase test accounts";
            public bool performanceExcluded=true,physicalDeviceTested=false;
            public List<CheckResult> checks=new List<CheckResult>();public List<string> errors=new List<string>();
        }
        sealed class Command{public string action,code;public TaskCompletionSource<string> reply=new TaskCompletionSource<string>();}
        readonly ConcurrentQueue<Command> commands=new ConcurrentQueue<Command>();
        readonly Report report=new Report();
        HttpListener listener;LabRuntime app;AccountService second,first;string output,root,csrf,pinA,userA,userB;
        string message="Starting Unity…";bool working,ready,verifiedA,verifiedA2,verifiedB,finished,cleaned;int sendCount;
        ProgressSnapshot progressA;

        async void Start()
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/non-performance-verification"));Directory.CreateDirectory(output);
            string previousPath=Path.Combine(output,"live-account-report.json");
            var previous=File.Exists(previousPath)?JsonUtility.FromJson<Report>(File.ReadAllText(previousPath)):null;
            try
            {
                await Wait(()=>LabRuntime.Instance&&LabRuntime.Instance.Account!=null,"Unity account initialization");app=LabRuntime.Instance;
                root=Path.Combine(Application.persistentDataPath,"Learning-v1");
                Check("isolated_persistent_root",Application.productName=="ThoraxLabAccountVerification",Application.persistentDataPath);
                second=new AccountService(Path.Combine(root,"independent-session"));
                csrf=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
                byte[] random=new byte[4];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);
                pinA=(BitConverter.ToUInt32(random,0)%1000000).ToString("D6");
                listener=new HttpListener();listener.Prefixes.Add(Origin+"/");listener.Start();_ = Listen();
                // An unauthenticated test-runner restart need not invalidate an
                // already sent OTP. Reopen the real UI's code-entry screen only.
                if(previous!=null&&previous.checks.Any(c=>c.id=="unity_ui_requests_email_code")&&!previous.checks.Any(c=>c.id=="unity_ui_email_code_sign_in"))
                {
                    typeof(LabUI).GetField("email",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(app.UI,EmailA);
                    typeof(LabUI).GetMethod("ShowCode",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(app.UI,null);
                    report.checks.Add(new CheckResult{id="unity_ui_requests_email_code",passed=true,detail="Retained successful pre-restart email request."});
                    message="Enter the code already sent to account A. If it has expired, request a new code.";
                }
                else message="Ready. Request the code for test account A.";
                ready=true;Save();
                File.WriteAllText(Path.Combine(output,"portal-ready.json"),"{\"url\":\""+Origin+"/\",\"ready\":true}");
            }
            catch(Exception ex){Error(ex);}
        }
        async Task Listen()
        {
            while(listener!=null&&listener.IsListening)
            {
                HttpListenerContext context;
                try{context=await listener.GetContextAsync().ConfigureAwait(false);}catch{return;}
                _=Handle(context);
            }
        }
        async Task Handle(HttpListenerContext context)
        {
            try
            {
                if(context.Request.Url.Host!="127.0.0.1"){context.Response.StatusCode=403;return;}
                string html;
                if(context.Request.HttpMethod=="POST"&&context.Request.Url.AbsolutePath=="/command")
                {
                    if(context.Request.Headers["Origin"]!=Origin||context.Request.ContentLength64>2048){context.Response.StatusCode=403;return;}
                    string body;using(var reader=new StreamReader(context.Request.InputStream))body=await reader.ReadToEndAsync().ConfigureAwait(false);
                    var fields=body.Split('&').Select(p=>p.Split(new[]{'='},2)).Where(p=>p.Length==2).ToDictionary(p=>WebUtility.UrlDecode(p[0]),p=>WebUtility.UrlDecode(p[1]));
                    if(!fields.TryGetValue("csrf",out var token)||token!=csrf){context.Response.StatusCode=403;return;}
                    var command=new Command{action=fields.TryGetValue("action",out var action)?action:"",code=fields.TryGetValue("code",out var code)?code:""};
                    commands.Enqueue(command);html=await command.reply.Task.ConfigureAwait(false);
                }
                else if(context.Request.HttpMethod=="GET"&&context.Request.Url.AbsolutePath=="/")html=Page();
                else{context.Response.StatusCode=404;return;}
                context.Response.Headers["Cache-Control"]="no-store";context.Response.Headers["X-Frame-Options"]="DENY";
                context.Response.Headers["Content-Security-Policy"]="default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'";
                var bytes=Encoding.UTF8.GetBytes(html);context.Response.ContentType="text/html; charset=utf-8";await context.Response.OutputStream.WriteAsync(bytes,0,bytes.Length).ConfigureAwait(false);
            }
            catch{context.Response.StatusCode=500;}
            finally{context.Response.Close();}
        }
        void Update(){if(ready&&!working&&commands.TryDequeue(out var command))_ = Process(command);}
        async Task Process(Command command)
        {
            working=true;
            try
            {
                switch(command.action)
                {
                    case "send-a":
                        if(verifiedA)throw new InvalidOperationException("Account A is already verified.");
                        app.UI.ShowSignIn();await Task.Delay(100);Field("Email address").text=EmailA;Click("Send email code");
                        await Wait(()=>!app.Account.Busy&&HasField("Six-digit code"),"sending account A code");
                        Check("unity_ui_requests_email_code",app.Account.CodeExpiresUtc>DateTime.UtcNow, "First-time account creation is enabled by the production request.");
                        sendCount++;message="Code sent to "+EmailA+". Enter it below.";Capture("sign-in-code-screen");break;
                    case "verify-a":
                        if(verifiedA)throw new InvalidOperationException("Account A is already verified.");
                        Field("Six-digit code").text=command.code;Click("Verify and continue");
                        await Wait(()=>!app.Account.Busy&&app.Account.UserId!=null,"verifying account A");
                        Check("unity_ui_email_code_sign_in",app.Account.Email==EmailA&&app.Account.UserId!=null&&app.Progress.userId==app.Account.UserId);
                        userA=app.Account.UserId;app.Account.TrustDevice(pinA);first=new AccountService(root);first.UnlockOffline(userA,pinA);await first.EnsureOnline();
                        verifiedA=true;await LocalChecks();message="Account A signed in inside Unity. Request a second code for its independent session.";Capture("signed-in-career-hub");break;
                    case "send-a2":
                        if(!verifiedA||verifiedA2)throw new InvalidOperationException("Complete the first account A sign-in first.");
                        await second.SendCode(EmailA);sendCount++;
                        bool resendBlocked=false;try{await second.SendCode(EmailA);}catch(InvalidOperationException ex){resendBlocked=ex.Message.Contains("wait");}
                        Check("client_resend_interval_enforced",resendBlocked);
                        message="Second code sent to account A. This creates a separate authenticated session.";break;
                    case "verify-a2":
                        await second.VerifyCode(EmailA,command.code);Check("second_independent_auth_session",second.UserId==userA&&second.Session.refresh_token!=first.Session.refresh_token);
                        verifiedA2=true;await SyncChecks();message="Both sessions verified and cloud tests complete. Request the code for test account B.";break;
                    case "send-b":
                        if(!verifiedA2||verifiedB)throw new InvalidOperationException("Verify both account A sessions first.");
                        app.Account.SignOut();app.UI.ShowSignIn();await Task.Delay(100);Field("Email address").text=EmailB;Click("Send email code");
                        await Wait(()=>!app.Account.Busy&&HasField("Six-digit code"),"sending account B code");sendCount++;message="Code sent to "+EmailB+". Enter it below.";break;
                    case "verify-b":
                        Field("Six-digit code").text=command.code;Click("Verify and continue");await Wait(()=>!app.Account.Busy&&app.Account.UserId!=null,"verifying account B");
                        userB=app.Account.UserId;verifiedB=true;await IsolationChecks();message="Live verification completed. Results are recorded below. Test accounts are retained; no account was deleted.";
                        finished=true;report.status=report.checks.All(c=>c.passed)&&report.errors.Count==0?"passed":"failed";Capture("second-account-isolation");break;
                    case "cleanup":
                        if(!finished||cleaned||first.Email!=EmailA||app.Account.Email!=EmailB)throw new InvalidOperationException("Only the two completed, approved QA accounts may be deleted.");
                        var stale=JsonUtility.FromJson<ProgressSnapshot>(JsonUtility.ToJson(progressA));int generation=stale.resetGeneration;
                        await first.ResetProgress(progressA);await second.Sync(stale);
                        Check("stale_independent_session_cannot_restore_reset",stale.resetGeneration==generation+1&&stale.attempts.Count==0&&stale.rewards.Count==0&&!stale.orientationCompleted);
                        await app.Account.ResetProgress(app.Progress);Check("second_test_account_reset",app.Progress.attempts.Count==0&&!app.Progress.orientationCompleted);
                        await first.DeleteAccount();Check("first_account_delete_clears_local_trust",first.Session==null&&!first.Trusted.Accounts().Any(a=>a.userId==userA));
                        bool deletedUserDenied=false;try{await second.Request("GET","/auth/v1/user",null,true);}catch(InvalidOperationException){deletedUserDenied=true;}
                        Check("deleted_account_rejected_by_auth",deletedUserDenied);
                        second.Session.expires_at=0;bool refreshDenied=false;try{await second.EnsureOnline();}catch(Exception){refreshDenied=true;}
                        Check("deleted_account_refresh_token_revoked",refreshDenied);
                        await app.Account.DeleteAccount();Check("second_account_delete_clears_application",app.Account.Session==null&&app.Progress.userId=="preview");
                        cleaned=true;message="Both temporary accounts were reset and deleted. Your normal account was not used.";report.status=report.errors.Count==0?"passed":"failed";break;
                    case "close":if(!finished)throw new InvalidOperationException("Finish verification before closing.");message="Verification saved. You can close this page.";break;
                    default:throw new InvalidOperationException("Unknown verification action.");
                }
            }
            catch(Exception ex){Error(ex);}
            finally{command.code=null;working=false;Save();command.reply.TrySetResult(Page());}
            if(command.action=="close"&&finished){await Task.Delay(500);LearningAccountVerification.Close();}
        }
        async Task LocalChecks()
        {
            var trusted=Path.Combine(root,"trusted",userA+".device");var data=Encoding.UTF8.GetString(File.ReadAllBytes(trusted));
            Check("dpapi_file_contains_no_plaintext_tokens",!data.Contains(first.Session.access_token)&&!data.Contains(first.Session.refresh_token)&&!data.Contains(EmailA));
            var restarted=new AccountService(root);bool wrong=false;try{restarted.UnlockOffline(userA,"wrong!");}catch(InvalidOperationException){wrong=true;}
            Check("incorrect_local_pin_denied",wrong&&restarted.Session==null);
            restarted.UnlockOffline(userA,pinA);Check("offline_pin_after_service_restart",restarted.Offline&&restarted.UserId==userA);
            bool resetDenied=false;try{restarted.TrustDevice(pinA);}catch(InvalidOperationException){resetDenied=true;}
            Check("offline_pin_reset_requires_email_auth",resetDenied);
            await restarted.EnsureOnline();Check("offline_session_refresh_reconnects",!restarted.Offline&&restarted.UserId==userA);
            bool malformed=false;try{await restarted.VerifyCode(EmailA,"abc");}catch(ArgumentException){malformed=true;}
            Check("malformed_code_rejected_locally",malformed);
            bool wrongOtp=false;try{await restarted.VerifyCode(EmailA,"000000");}catch(InvalidOperationException){wrongOtp=true;}
            Check("incorrect_email_code_denied",wrongOtp&&restarted.UserId==userA);
        }
        async Task SyncChecks()
        {
            progressA=new ProgressSnapshot{userId=userA};await first.Sync(progressA);
            Check("fresh_test_account_has_no_existing_progress",progressA.attempts.Count==0&&!progressA.orientationCompleted);
            if(progressA.attempts.Count!=0||progressA.orientationCompleted)throw new InvalidOperationException("Test account already has data; refusing to overwrite its history.");
            progressA.orientationCompleted=true;progressA.pendingSync=true;
            var variant=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
            var attempt=Completed(variant,userA,"qa-independent-session-a",progressA.resetGeneration);ProgressRules.AddAttempt(progressA,attempt);
            await first.Sync(progressA);int rewards=progressA.rewards.Count;
            Check("live_completed_attempt_and_mastery",progressA.attempts.Single().passed&&progressA.attempts.Single().synced&&ProgressRules.IsUnlocked(progressA,variant.mode,variant.unitId,Difficulty.Medium));
            progressA.attempts.Single().synced=false;await first.Sync(progressA);Check("repeated_sync_does_not_duplicate_rewards",progressA.attempts.Count==1&&progressA.rewards.Count==rewards);
            var other=new ProgressSnapshot{userId=userA};await second.Sync(other);Check("independent_session_loads_cloud_progress",other.attempts.Count==1&&other.rewards.Count==rewards&&other.orientationCompleted);
            var secondAttempt=Completed(variant,userA,"qa-independent-session-b",other.resetGeneration);ProgressRules.AddAttempt(other,secondAttempt);
            await Task.WhenAll(first.Sync(progressA),second.Sync(other));await first.Sync(progressA);
            Check("concurrent_sessions_retain_separate_attempts",progressA.attempts.Count==2&&progressA.rewards.Count==rewards);
            var store=ProgressStore(Path.Combine(root,"verification-progress.save"),userA);store.Save(progressA);store.Save(progressA);File.WriteAllText(store.FilePath,"simulated interrupted write");
            var recovered=store.Load(()=>new ProgressSnapshot{userId=userA});Check("real_account_backup_recovery",store.RecoveredBackup&&recovered.attempts.Count==2&&recovered.rewards.Count==rewards);
            var partial=NewEngine(variant,userA,"qa-independent-session-a",progressA.resetGeneration,SessionPhase.Practice);ApplyObjective(partial,partial.Current);
            var checkpoint=Checkpoint(partial);progressA.checkpoints.Add(checkpoint);ProgressRules.AddAttempt(progressA,partial.Attempt);await first.Sync(progressA);await second.Sync(other);
            Check("safe_checkpoint_reaches_second_session",other.checkpoints.Any(c=>c.attemptId==partial.Attempt.id)&&other.attempts.Single(a=>a.id==partial.Attempt.id).actions.Count==1);
            ApplyObjective(partial,partial.Current);checkpoint=Checkpoint(partial);progressA.checkpoints.RemoveAll(c=>c.attemptId==partial.Attempt.id);progressA.checkpoints.Add(checkpoint);await first.Sync(progressA);await second.Sync(other);
            Check("newer_checkpoint_updates_cached_attempt",other.attempts.Single(a=>a.id==partial.Attempt.id).actions.Count==2);
            var profile=new SettingsProfile{name="Temporary account verification",platform="Windows"};
            await first.SyncProfiles(profile);var remoteProfiles=await second.SyncProfiles();Check("profile_sync_between_sessions",remoteProfiles.Any(p=>p.id==profile.id&&p.name==profile.name));
            await second.SyncProfiles(deleteId:profile.id);Check("account_profile_delete_persists",!(await first.SyncProfiles()).Any(p=>p.id==profile.id));
            // Account roots and tokens remain outside reports; exporting uses the actual game path.
            await app.Synchronize();var exported=app.ExportProgress();var exportText=File.ReadAllText(exported);
            Check("progress_export_excludes_auth_secrets",!exportText.Contains(first.Session.access_token)&&!exportText.Contains("refresh_token")&&exportText.Contains(userA));
            app.Account.SignOut();Check("sign_out_clears_session_and_visible_progress",app.Account.Session==null&&app.Progress.userId=="preview");
            app.Account.UnlockOffline(userA,pinA);Check("actual_app_offline_resume",app.Account.Offline&&app.Progress.userId==userA&&app.Progress.attempts.Count>=2);
        }
        async Task IsolationChecks()
        {
            await Wait(()=>!app.Account.Busy,"account B initial sync");await app.Synchronize();
            Check("account_switch_has_isolated_progress",userB!=userA&&app.Progress.userId==userB&&app.Progress.attempts.Count==0&&!app.Progress.orientationCompleted);
            var rows=await app.Account.Request("GET","/rest/v1/thorax_attempts?select=id&user_id=eq."+userA,null,true);
            Check("rls_hides_other_account_attempts",rows.Trim()=="[]");
            bool crossClient=false;try{await app.Account.Sync(progressA);}catch(InvalidOperationException){crossClient=true;}
            Check("client_rejects_foreign_progress",crossClient);
            var payload=new IsolationPayload{expected_generation=app.Progress.resetGeneration,orientation_completed=true,incoming_attempts=progressA.attempts.Where(a=>a.status==AttemptStatus.Completed).Take(1).ToArray()};
            bool serverDenied=false;try{await app.Account.Request("POST","/rest/v1/rpc/thorax_sync_progress",payload,true);}catch(Exception){serverDenied=true;}
            Check("server_rejects_foreign_account_attempt",serverDenied);
            await app.Synchronize();Check("rejected_foreign_attempt_does_not_change_b",app.Progress.attempts.Count==0&&!app.Progress.orientationCompleted);
            var beforeProfiles=await first.SyncProfiles();var repeatedProfiles=await first.SyncProfiles();
            Check("account_profile_reads_do_not_create_defaults",beforeProfiles.Length==0&&repeatedProfiles.Length==0);await first.Sync(progressA);Check("account_a_attempts_preserved",progressA.attempts.Count(a=>a.status==AttemptStatus.Completed)==2);
        }
        [Serializable] sealed class IsolationPayload{public int expected_generation;public bool orientation_completed;public AttemptRecord[] incoming_attempts;public ActivityCheckpoint[] incoming_checkpoints=Array.Empty<ActivityCheckpoint>();}
        static AssessmentEngine NewEngine(ScenarioVariant v,string user,string device,int generation,SessionPhase phase=SessionPhase.Assessment)=>new AssessmentEngine(v,new AttemptRecord{userId=user,deviceId=device,scenarioId=v.id,unitId=v.unitId,contentVersion=v.version,mode=v.mode,difficulty=v.difficulty,phase=phase,resetGeneration=generation});
        static void ApplyObjective(AssessmentEngine engine,ObjectiveDefinition objective){if(objective!=null)engine.Apply(new InteractionAction{kind=objective.action,targetId=objective.targetId,toolId=objective.toolId,equipmentState=objective.equipmentState,contactVerified=true,contactSeconds=objective.minContactSeconds,pathFraction=objective.minPathFraction});}
        static AttemptRecord Completed(ScenarioVariant v,string user,string device,int generation){var engine=NewEngine(v,user,device,generation);foreach(var o in v.objectives)ApplyObjective(engine,o);engine.Finish();return engine.Attempt;}
        static ActivityCheckpoint Checkpoint(AssessmentEngine e)=>new ActivityCheckpoint{attemptId=e.Attempt.id,scenarioId=e.Attempt.scenarioId,phase=e.Attempt.phase,resetGeneration=e.Attempt.resetGeneration,savedUtc=DateTime.UtcNow.ToString("o"),completedObjectiveIds=e.Attempt.objectives.Where(o=>o.completed).Select(o=>o.objectiveId).ToArray(),attempt=e.Attempt};
        static AtomicFileStore<ProgressSnapshot> ProgressStore(string path,string user)=>new AtomicFileStore<ProgressSnapshot>(path,p=>JsonUtility.ToJson(p),JsonUtility.FromJson<ProgressSnapshot>,p=>{if(p==null||p.userId!=user)throw new InvalidDataException("Account mismatch.");});
        TMP_InputField Field(string name)=>app.UI.GetComponentsInChildren<TMP_InputField>().Single(f=>f.gameObject.name==name);
        bool HasField(string name)=>app.UI.GetComponentsInChildren<TMP_InputField>().Any(f=>f.gameObject.name==name);
        void Click(string name)=>app.UI.GetComponentsInChildren<Button>().Single(b=>b.gameObject.name==name).onClick.Invoke();
        async Task Wait(Func<bool> predicate,string label){var until=DateTime.UtcNow.AddSeconds(45);while(!predicate()){if(DateTime.UtcNow>until)throw new TimeoutException(label+" did not finish. "+(app?app.Notice:""));await Task.Delay(100);}}
        void Check(string id,bool passed,string detail=""){report.checks.Add(new CheckResult{id=id,passed=passed,detail=detail});Save();if(!passed)throw new InvalidOperationException("Verification failed: "+id);}
        void Error(Exception ex){message=ex.Message;report.errors.Add(ex.GetType().Name+": "+ex.Message);report.status="needs attention";Save();}
        void Save(){if(output==null)return;report.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(Path.Combine(output,"live-account-report.json"),JsonUtility.ToJson(report,true));}
        void Capture(string name){var camera=Camera.main;if(!camera)return;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1440,1000,24);var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,1000),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;Destroy(rt);Destroy(texture);}}
        string Page()
        {
            string E(string value)=>WebUtility.HtmlEncode(value);
            string Form(string action,string label,bool code=false)=>"<form method='post' action='/command'><input type='hidden' name='csrf' value='"+csrf+"'><input type='hidden' name='action' value='"+action+"'>"+(code?"<label>Six-digit email code<input type='password' name='code' inputmode='numeric' pattern='[0-9]{6}' maxlength='6' autocomplete='one-time-code' required></label>":"")+"<button>"+E(label)+"</button></form>";
            return "<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Anatome account verification</title><style>body{font:18px system-ui;background:#0c151d;color:#edece7;max-width:750px;margin:40px auto;padding:20px}h1{font-size:30px}p{line-height:1.6}.status{background:#173331;padding:20px;border-radius:12px}button,input{font:inherit;padding:14px;border-radius:8px;border:1px solid #365560}button{background:#45cbb6;color:#071617;cursor:pointer;margin:12px 8px 16px 0}input{display:block;margin-top:8px;background:#15232d;color:white}li{margin-bottom:10px}small{color:#a2b8be}</style><h1>Anatome account verification</h1><p>This local page forwards your code to the running Unity game's sign-in handlers. Codes and session tokens are not written to reports. VR performance is excluded.</p><p class='status'>"+E(message)+"</p>"+
            (!verifiedA?"<h2>1 · Test account A</h2><p>"+EmailA+"</p>"+Form("send-a","Send account A code")+Form("verify-a","Verify account A",true):!verifiedA2?"<h2>2 · Independent session for account A</h2><p>"+EmailA+"</p>"+Form("send-a2","Send second-session code")+Form("verify-a2","Verify second session",true):!verifiedB?"<h2>3 · Test account B</h2><p>"+EmailB+"</p>"+Form("send-b","Send account B code")+Form("verify-b","Verify account B",true):(!cleaned?Form("cleanup","Reset and delete the two approved test accounts"):"")+Form("close","Finish and close verification"))+
            "<h2>Verification results</h2><ul>"+string.Join("",report.checks.Select(c=>"<li>"+(c.passed?"PASS · ":"FAIL · ")+E(c.id.Replace('_',' '))+"</li>"))+"</ul><small>Only the two approved temporary test accounts are used. "+sendCount+" code requests sent.</small>";
        }
        void OnDestroy(){listener?.Close();listener=null;}
    }
}
#endif

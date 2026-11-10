#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEngine;

namespace ThoraxLab.Editor
{
    public static class AccountCloudRecoveryVerification
    {
        [Serializable] sealed class Result{public string utc,status;public List<string> passed=new List<string>();public List<string> errors=new List<string>();public int profilesBefore,oldGeneration,newGeneration;}
        public static async void Run()
        {
            var result=new Result{utc=DateTime.UtcNow.ToString("o")};
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow",LearningBrand.Name,"ThoraxLabAccountVerification","Learning-v1");
            void Check(string id,bool passed){if(!passed)throw new InvalidOperationException(id);result.passed.Add(id);}
            try
            {
                var service=new AccountService(root);var trusted=service.Trusted.Accounts().Single(a=>a.email=="j.oginam2+thorax-qa-a@gmail.com");
                // Reuse only the approved QA session, encrypted by the actual
                // trusted-device flow. This is test setup, not a PIN-unlock test.
                var accept=typeof(AccountService).GetMethod("AcceptSession",BindingFlags.Instance|BindingFlags.NonPublic);
                accept.Invoke(service,new object[]{trusted.session});service.Session.expires_at=0;await service.EnsureOnline();
                var before=await service.SyncProfiles();result.profilesBefore=before.Length;
                var after=await service.SyncProfiles();Check("repeated_profile_read_is_read_only",before.Select(p=>p.id).OrderBy(x=>x).SequenceEqual(after.Select(p=>p.id).OrderBy(x=>x)));
                foreach(var profile in before)await service.SyncProfiles(deleteId:profile.id);
                Check("profile_delete_creates_no_replacement",(await service.SyncProfiles()).Length==0);
                var progress=new ProgressSnapshot{userId=service.UserId};await service.Sync(progress);
                Check("isolation_test_preserved_account_a_attempts",progress.attempts.Count(a=>a.status==AttemptStatus.Completed)==2);
                var stale=JsonUtility.FromJson<ProgressSnapshot>(JsonUtility.ToJson(progress));result.oldGeneration=stale.resetGeneration;
                await service.ResetProgress(progress);result.newGeneration=progress.resetGeneration;
                Check("server_reset_increments_generation",progress.resetGeneration==stale.resetGeneration+1&&progress.attempts.Count==0&&progress.rewards.Count==0&&!progress.orientationCompleted);
                await service.Sync(stale);Check("stale_client_cannot_restore_reset_records",stale.resetGeneration==progress.resetGeneration&&stale.attempts.Count==0&&stale.checkpoints.Count==0&&stale.rewards.Count==0&&!stale.orientationCompleted);
                stale.pendingSync=true;stale.localRevision++;
                var store=new AtomicFileStore<ProgressSnapshot>(Path.Combine(root,"network-retry.save"),p=>JsonUtility.ToJson(p),JsonUtility.FromJson<ProgressSnapshot>,p=>{if(p==null||p.userId!=service.UserId)throw new InvalidDataException();});store.Save(stale);
                string original=JsonUtility.ToJson(stale);bool interrupted=false;
                service.VerificationRequestStarted=request=>request.Abort();
                try{await service.Sync(stale);}catch(IOException ex){interrupted=ex.Message.StartsWith("Cannot connect");}finally{service.VerificationRequestStarted=null;}
                Check("transport_interruption_preserves_pending_progress",interrupted&&JsonUtility.ToJson(stale)==original&&store.Load(()=>null).pendingSync);
                await service.Sync(stale);Check("reconnection_retries_pending_sync",!stale.pendingSync&&stale.lastSyncedUtc!=null);
                var session=service.Session;var pending=service.Sync(stale);service.SignOut();bool cancelled=false;
                try{await pending;}catch(InvalidOperationException ex){cancelled=ex.Message.Contains("Account changed");}
                Check("sign_out_cancels_inflight_sync_application",cancelled&&service.Session==null);
                accept.Invoke(service,new object[]{session});await service.Sync(stale);Check("account_reconnects_after_cancelled_sync",stale.attempts.Count==0&&stale.resetGeneration==result.newGeneration);
                result.status="passed";
            }
            catch(Exception ex){result.errors.Add(ex.GetType().Name+": "+ex.Message);result.status="failed";}
            result.utc=DateTime.UtcNow.ToString("o");var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/non-performance-verification/cloud-recovery-report.json"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.Log("Cloud recovery verification: "+result.status+" ("+result.passed.Count+" checks)");EditorApplication.Exit(result.status=="passed"?0:1);
        }
    }
}
#endif

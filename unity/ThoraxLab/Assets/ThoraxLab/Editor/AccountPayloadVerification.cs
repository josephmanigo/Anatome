#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEngine;

namespace ThoraxLab.Editor
{
    public static class AccountPayloadVerification
    {
        [Serializable] sealed class Report { public string utc,readPayload,deletePayload;public bool readOmitsProfile,deleteOmitsProfile,saveContainsRequestedProfile,passed; }
        public static void Run()
        {
            var build=typeof(AccountService).GetMethod("ProfileSyncPayload",BindingFlags.Static|BindingFlags.NonPublic);
            string Encode(SettingsProfile profile,string id)=>JsonUtility.ToJson(build.Invoke(null,new object[]{profile,id}));
            var report=new Report{utc=DateTime.UtcNow.ToString("o"),readPayload=Encode(null,null),deletePayload=Encode(null,"test-profile")};
            report.readOmitsProfile=!report.readPayload.Contains("incoming_profile");report.deleteOmitsProfile=!report.deletePayload.Contains("incoming_profile")&&report.deletePayload.Contains("test-profile");
            var profile=new SettingsProfile{name="Requested profile"};var save=Encode(profile,null);report.saveContainsRequestedProfile=save.Contains(profile.id)&&save.Contains("Requested profile");
            report.passed=report.readOmitsProfile&&report.deleteOmitsProfile&&report.saveContainsRequestedProfile;
            var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/non-performance-verification/profile-payload-report.json"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log("Settings payload regression: "+(report.passed?"PASS":"FAIL"));
            if(!report.passed)throw new InvalidOperationException("Profile reads/deletes unexpectedly contain a generated profile.");
        }
    }
}
#endif

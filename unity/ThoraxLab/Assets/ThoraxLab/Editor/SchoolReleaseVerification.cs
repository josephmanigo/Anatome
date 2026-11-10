#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ThoraxLab.Learning;
using UnityEngine;

namespace ThoraxLab.Editor
{
    public static class SchoolReleaseVerification
    {
        [Serializable] sealed class Report { public string utc; public bool passed; public List<string> checks=new(); public List<string> errors=new(); }
        public static void Run()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("o")};
            void Check(bool valid,string name){if(!valid)throw new Exception(name);report.checks.Add(name);}
            try
            {
                string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
                string fixture=Path.Combine(root,".tmp","school-manifest-"+Guid.NewGuid().ToString("N"));
                string manifest=Path.Combine(fixture,"src/main/AndroidManifest.xml");Directory.CreateDirectory(Path.GetDirectoryName(manifest));
                File.WriteAllText(manifest,"<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application android:allowBackup=\"true\"><meta-data android:name=\"com.oculus.supportedDevices\" android:value=\"quest2|eureka\"/><activity android:name=\"ExistingActivity\"/></application></manifest>");
                LearningAndroidPolicy.Configure(fixture);LearningAndroidPolicy.Configure(fixture);
                var app=XDocument.Load(manifest).Root.Element("application");var android=LearningAndroidPolicy.Android;
                foreach(string attribute in new[]{"allowBackup","fullBackupContent","usesCleartextTraffic"})Check((string)app.Attribute(android+attribute)=="false",attribute+" disabled");
                Check((string)app.Attribute(android+"dataExtractionRules")=="@xml/anatome_data_extraction_rules","Backup resource linked");
                Check(app.Elements("activity").Count()==1,"Existing Android activity retained");
                Check(app.Elements("meta-data").Count()==1,"Repeated build does not duplicate supported devices");
                Check((string)app.Element("meta-data").Attribute(android+"value")=="quest2|questpro|quest3|quest3s","Explicit Quest 2 and Quest 3 device identifiers");
                Check((string)app.Element("meta-data").Attribute(XNamespace.Get("http://schemas.android.com/tools")+"replace")=="android:value","Device identifiers override the generated XR library during manifest merge");
                var rules=XDocument.Load(Path.Combine(fixture,"src/main/res/xml/anatome_data_extraction_rules.xml"));
                foreach(string mode in new[]{"cloud-backup","device-transfer"})
                {
                    var entries=rules.Root.Element(mode).Elements("exclude").ToArray();
                    Check(entries.Length==9&&LearningAndroidPolicy.BackupDomains.All(domain=>entries.Any(e=>(string)e.Attribute("domain")==domain&&(string)e.Attribute("path")==".")),mode+" excludes every app storage domain");
                }
                var configuration=JsonUtility.FromJson<SupabaseConfiguration>(Resources.Load<TextAsset>("Learning/supabase-config").text);
                LearningAndroidPolicy.ValidateConfiguration(configuration);report.checks.Add("Packaged account key is public");
                string original=configuration.publishableKey;
                foreach(string invalid in new[]{"sb_secret_do_not_ship_this_private_key","header."+Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"role\":\"service_role\"}"))+".signature","invalid","header.!.signature"})
                {
                    configuration.publishableKey=invalid;bool rejected=false;
                    try{LearningAndroidPolicy.ValidateConfiguration(configuration);}catch(InvalidDataException){rejected=true;}
                    Check(rejected,"Private or malformed client key rejected "+report.checks.Count);
                }
                configuration.publishableKey=original;configuration.projectUrl="http://example.supabase.co";bool insecureRejected=false;
                try{LearningAndroidPolicy.ValidateConfiguration(configuration);}catch(InvalidDataException){insecureRejected=true;}
                Check(insecureRejected,"Cleartext account endpoint rejected");
                report.passed=true;
            }
            catch(Exception error){report.errors.Add(error.ToString());}
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/production-school-20261016/build-policy.json"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            if(!report.passed)throw new Exception(string.Join("\n",report.errors));
            Debug.Log("School build policy passed: "+report.checks.Count+" checks.");
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ThoraxLab.Learning;
using UnityEditor.Android;
using UnityEngine;

namespace ThoraxLab.Editor
{
#pragma warning disable CS0618
    public sealed class LearningAndroidPolicy : IPostGenerateGradleAndroidProject
#pragma warning restore CS0618
    {
        public int callbackOrder => int.MaxValue;
        internal static readonly string[] BackupDomains = { "root", "file", "database", "sharedpref", "external", "device_root", "device_file", "device_database", "device_sharedpref" };
        internal static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        public void OnPostGenerateGradleAndroidProject(string path) => Configure(path);

        internal static void Configure(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = XDocument.Load(manifest);
            var app = xml.Root?.Element("application") ?? throw new InvalidDataException("Android application manifest is missing.");
            app.SetAttributeValue(Android + "allowBackup", "false");
            app.SetAttributeValue(Android + "fullBackupContent", "false");
            app.SetAttributeValue(Android + "dataExtractionRules", "@xml/anatome_data_extraction_rules");
            app.SetAttributeValue(Android + "usesCleartextTraffic", "false");
            var devices = app.Elements("meta-data").FirstOrDefault(e => (string)e.Attribute(Android + "name") == "com.oculus.supportedDevices");
            if (devices == null) { devices = new XElement("meta-data", new XAttribute(Android + "name", "com.oculus.supportedDevices")); app.Add(devices); }
            devices.SetAttributeValue(Android + "value", "quest2|questpro|quest3|quest3s");
            XNamespace tools = "http://schemas.android.com/tools";
            xml.Root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
            devices.SetAttributeValue(tools + "replace", "android:value");
            xml.Save(manifest);
            string resources = Path.Combine(path, "src/main/res/xml"); Directory.CreateDirectory(resources);
            // Device-bound Keystore tokens must never be restored onto a different headset.
            var rules = new XElement("data-extraction-rules");
            foreach (string mode in new[] { "cloud-backup", "device-transfer" })
                rules.Add(new XElement(mode, BackupDomains.Select(domain => new XElement("exclude", new XAttribute("domain", domain), new XAttribute("path", ".")))));
            new XDocument(rules).Save(Path.Combine(resources, "anatome_data_extraction_rules.xml"));
        }

        [Serializable] sealed class KeyClaims { public string role; }
        internal static void ValidateConfiguration(SupabaseConfiguration configuration)
        {
            if (configuration == null || !configuration.Ready) throw new InvalidDataException("A valid HTTPS account configuration is required for the release.");
            string key = configuration.publishableKey;
            if (key.StartsWith("sb_publishable_", StringComparison.Ordinal) && key.Length > 24) return;
            try
            {
                string[] parts = key.Split('.');
                if (parts.Length != 3) throw new FormatException();
                string claims = parts[1].Replace('-', '+').Replace('_', '/');
                claims = claims.PadRight((claims.Length + 3) / 4 * 4, '=');
                if (JsonUtility.FromJson<KeyClaims>(Encoding.UTF8.GetString(Convert.FromBase64String(claims)))?.role == "anon") return;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException) { }
            throw new InvalidDataException("Only a public publishable or anon account key may be packaged. Private service keys are forbidden.");
        }
    }
}
#endif

using System;
using System.IO;

namespace ThoraxLab.Learning
{
    public static class LearningBrand
    {
        public const string Name = "Anatome";
        public const string LegacyCompany = "Thorax Lab";
        public const string LegacyProduct = "Thorax Lab";

        public static string DataRoot(string persistentPath, bool windows, string company, string product)
        {
            // Unity derives Windows persistentDataPath from display metadata.
            // Keep the established directory for existing and new installations
            // so changing branding cannot strand progress or trusted credentials.
            // Test products deliberately keep their separate Unity directory.
            if (windows && company == Name && product == Name)
            {
                var companyDirectory = Directory.GetParent(persistentPath);
                var localLow = companyDirectory?.Parent;
                if (localLow == null) throw new InvalidDataException("Cannot locate the Windows learning data directory.");
                persistentPath = Path.Combine(localLow.FullName, LegacyCompany, LegacyProduct);
            }
            return Path.Combine(persistentPath, "Learning-v1");
        }
    }
}

using System;
using System.Configuration;
using static System.Reflection.BindingFlags;

namespace Converter.Configuration
{
    public class Configuration
    {

        public string FullName => "[" + HelperSchema + "].[" + HelperTableName + "]";

        [StSetupFromConfig(true)] public string EpName { get; set; } = "MOIndexEp";

        [StSetupFromConfig(true)] public string HelperTableName { get; set; } = "TestTable";


        [StSetupFromConfig(true)] public string HelperSchema { get; set; } = "dbo";


        [StSetupFromConfig(true)] public string FileGroupName { get; set; } = "mofg";

        [StSetupFromConfig(true)] public string FileName { get; set; } = "mofile";


        public string MMoPath = @"C:\MOCONTAINTER";
        [StSetupFromConfig(true)]
        public string MoPath
        {
            get => MMoPath;
            set => MMoPath = value + DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");
        }


        public void LoadConfig()
        {
            foreach (var objectProperty in GetType().GetProperties((IgnoreCase
                            | (Instance
                            | (NonPublic | Public)))))
            {
                if ((objectProperty.GetCustomAttributes(typeof(StSetupFromConfig), true).Length <= 0)) continue;
                try
                {
                    string a = objectProperty.Name.ToUpper();
                    objectProperty.SetValue(this,
                        Convert.ChangeType(ConfigurationManager.AppSettings[a], objectProperty.PropertyType),
                        Public, null, null, null);
                }
                catch (Exception)
                {
                    //if (Debugger.IsAttached)
                    //    Debugger.Break();
                }

            }


        }


    }

    [System.AttributeUsage(AttributeTargets.Property)]
    public class StSetupFromConfig : Attribute
    {
        public StSetupFromConfig(bool wValueFromConfig) => ValueFromConfig = wValueFromConfig;

        public bool ValueFromConfig { get; set; } = false;
    }
}

using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;

namespace Converter.Configuration
{
    public class Configuration
    {

        public string fullName
        {
            get { return "[" + mHelperSchema + "].[" + mHelperTableName + "]"; }
        }

        private string mEPName = "MOIndexEp";
        [StSetupFromConfig(true)]
        public string EPName
        {
            get { return mEPName; }
            set { mEPName = value; }
        }

        private  string mHelperTableName = "TestTable";
        [StSetupFromConfig(true)]
        public string helperTableName
        {
            get { return mHelperTableName; }
            set { mHelperTableName = value; }
        }


        private  string mHelperSchema = "dbo";
        [StSetupFromConfig(true)]
        public string helperSchema
        {
            get { return mHelperSchema; }
            set { mHelperSchema = value; }
        }



        private  string mFileGroupName = "mofg";
        [StSetupFromConfig(true)]
        public string FileGroupName
        {
            get { return mFileGroupName; }
            set { mFileGroupName = value; }
        }

        private string mFileName = "mofile";
        [StSetupFromConfig(true)]
        public  string FileName
        {
            get { return mFileName; }
            set { mFileName = value; }
        }


        private  string mMoPath = @"C:\MOCONTAINTER";
        [StSetupFromConfig(true)]
        public  string MoPath
        {
            get { return mMoPath; }
            set { mMoPath = value + DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss"); }
        }


        public void LoadConfig()
        {
            foreach (PropertyInfo objectProperty in this.GetType().GetProperties((BindingFlags.IgnoreCase
                            | (BindingFlags.Instance
                            | (BindingFlags.NonPublic | BindingFlags.Public)))))
            {
                if ((objectProperty.GetCustomAttributes(typeof(StSetupFromConfig), true).Length > 0))
                {
                    try
                    {
                        string a = objectProperty.Name.ToUpper();
                        objectProperty.SetValue(this, Convert.ChangeType(ConfigurationManager.AppSettings[a], objectProperty.PropertyType),BindingFlags.Public , null, null, null);
                    }
                    catch (Exception ex)
                    {
                        if (Debugger.IsAttached)
                            Debugger.Break();
                    }

                }

            }


        }


    }

    [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class StSetupFromConfig : System.Attribute
    {

        private bool m_valueFromConfig = false;

        public StSetupFromConfig(bool WValueFromConfig)
        {
            this.ValueFromConfig = WValueFromConfig;
        }

        public bool ValueFromConfig
        {
            get
            {
                return m_valueFromConfig;
            }
            set
            {
                m_valueFromConfig = value;
            }
        }
    }
}

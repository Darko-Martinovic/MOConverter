using Microsoft.SqlServer.Management.Smo;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Converter.Extension
{
    //public enum SfcPropertyFlags
    //{
    //    None = 0,
    //    Required = 16,
    //    Expensive = 32,
    //    Computed = 64,
    //    Encrypted = 128,
    //    ReadOnlyAfterCreation = 256,
    //    Data = 512,
    //    Standalone = 1024,
    //    SqlAzureDatabase = 2048,
    //    Design = 4096,
    //    Deploy = 8192
    //}
    public static class ObjectExtension
    {
        public static void CopyPropertiesFrom(this object self, object parent)
        {
            PropertyInfo[] fromProperties = parent.GetType().GetProperties();
            PropertyInfo toProperty;


            foreach (var fromProperty in fromProperties)
            {
                if (fromProperty.CanWrite == false)
                    continue;
                if (fromProperty.Name.ToLower().Equals("parent") || fromProperty.Name.ToLower().Equals("name"))
                    continue;
                switch (self)
                {
                    case Column _ when
                        $"GraphType_ColumnEncryptionKeyID_ColumnEncryptionKeyName_EncryptionAlgorithm_EncryptionType_DistributionColumnName_IsDistributedColumn"
                            .Contains(fromProperty.Name):
                    case UserDefinedFunction _ when
                        $"AssemblyName_ClassName_ExecutionContext_ExecutionContextPrincipal_FunctionType_IsEncrypted_IsSchemaBound_MethodName_TableVariableName"
                            .Contains(fromProperty.Name):
                    case StoredProcedure _ when
                        $"AssemblyName_ClassName_ExecutionContext_ExecutionContextPrincipal_ForReplication_IsEncrypted_MethodName_Recompile"
                            .Contains(fromProperty.Name):
                    case View _ when $"IsEncrypted_IsSchemaBound".Contains(fromProperty.Name):
                        continue;
                }


                try
                {
                    toProperty = self.GetType().GetProperty(fromProperty.Name);
                    toProperty?.SetValue(self, fromProperty.GetValue(parent));
                }
                catch (Exception ex)
                {
                    //if (Debugger.IsAttached)
                    //   Debugger.Break();
                }
            }

            fromProperties = null;
            toProperty = null;
        }

    }
}


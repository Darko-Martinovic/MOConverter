using Microsoft.SqlServer.Management.Smo;
using System;
using System.Reflection;

namespace Converter.Extension
{
    public static class ObjectExtension
    {


        public static void CopyPropertiesFrom(this object self, object parent)
        {
            var fromProperties = parent.GetType().GetProperties();
            PropertyInfo toProperty;


            foreach (var fromProperty in fromProperties)
            {
                if (fromProperty.CanWrite == false)
                    continue;
                if ("parent".Equals(fromProperty.Name.ToLower()) || fromProperty.Name.ToLower().Equals("name"))
                    continue;
                switch (self)
                {
                    case Column _ when
                        @"GraphType_ColumnEncryptionKeyID_ColumnEncryptionKeyName_EncryptionAlgorithm_EncryptionType_DistributionColumnName_IsDistributedColumn"
                            .Contains(fromProperty.Name):
                    case UserDefinedFunction _ when
                        @"AssemblyName_ClassName_ExecutionContext_ExecutionContextPrincipal_FunctionType_IsEncrypted_IsSchemaBound_MethodName_TableVariableName"
                            .Contains(fromProperty.Name):
                    case StoredProcedure _ when
                        @"AssemblyName_ClassName_ExecutionContext_ExecutionContextPrincipal_ForReplication_IsEncrypted_MethodName_Recompile"
                            .Contains(fromProperty.Name):
                    case View _ when @"IsEncrypted_IsSchemaBound".Contains(fromProperty.Name):
                        continue;
                }


                try
                {
                    toProperty = self.GetType().GetProperty(fromProperty.Name);
                    toProperty?.SetValue(self, fromProperty.GetValue(parent));
                }
                catch (Exception)
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


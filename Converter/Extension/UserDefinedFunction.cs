using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;


namespace Converter.Extension
{
    public static class UserDefinedFunctionExtension
    {
        internal static string FName(this UserDefinedFunction self) => "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";

        public static bool SwitchToMo(
                                     this UserDefinedFunction self, 
                                     Database inMemDatabase, 
                                     Database traditional, 
                                     Configuration.Configuration cnf, 
                                     ref string error, 
                                     ILog logger)
        {
            bool retValue = false;
            if (self.IsSystemObject)
                return true;

            if (inMemDatabase.UserDefinedFunctions.Contains(self.Name, self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }

            logger.Log("UDF", self.FName());
            UserDefinedFunction newsp = new UserDefinedFunction(inMemDatabase, self.Name, self.Schema);
            newsp.CopyPropertiesFrom(self);
            try
            {
                newsp.Create();
                logger.Log("OK", self.Name);
                retValue = true;
            }
            catch (Exception ex)
            {
                logger.Log("Error", self.FName());
                 error = string.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                    .Select(ex1 => ex1.Message));


            }
            newsp = null;
            return retValue;

        }
    }
}

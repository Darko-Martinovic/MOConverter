using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;


namespace Converter.Extension
{
    public static class UserDefinedFunctionExtension
    {
        private static string FName(this UserDefinedFunction self) =>
            $"{self.Parent.Name.BracketObjectName()}.{self.Schema.BracketObjectName()}.{self.Name.BracketObjectName()}";

        public static bool SwitchToMo(
                                     this UserDefinedFunction self, 
                                     Database inMemDatabase, 
                                     ref string error, 
                                     ILog logger)
        {
            var retValue = false;
            if (self.IsSystemObject)
                return true;

            if (inMemDatabase.UserDefinedFunctions.Contains(self.Name, self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }

            logger.Log("UDF", self.FName());
            var newsp = new UserDefinedFunction(inMemDatabase, self.Name, self.Schema);
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

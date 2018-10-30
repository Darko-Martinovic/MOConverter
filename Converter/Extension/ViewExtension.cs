using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Extension
{
    public static class ViewExtension
    {
        private static string FName(this View self) => $"{self.Parent.Name.BracketObjectName()}.{self.Schema.BracketObjectName()}.{self.Name.BracketObjectName()}";

        public static bool SwitchToMo(
                                     this View self, 
                                     Database inMemDatabase, 
                                     ref string error, 
                                     ILog logger
            )
        {
            var retValue = false;
            if (self.IsSystemObject)
                return true;

            if (inMemDatabase.Views.Contains(self.Name, self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }


            logger.Log("VIEW", self.FName());
            var newsp = new View(inMemDatabase, self.Name, self.Schema);
            newsp.CopyPropertiesFrom(self);
            try
            {
                newsp.Create();
                logger.Log("OK", self.FName());
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

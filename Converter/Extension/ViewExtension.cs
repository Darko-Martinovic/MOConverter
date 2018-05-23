using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Extension
{
    public static class ViewExtension
    {
        internal static string FName(this View self) => "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";

        public static bool SwitchToMo(
                                     this View self, 
                                     Database inMemDatabase, 
                                     Database traditional,
                                     Configuration.Configuration cnf, 
                                     ref string error, 
                                     ILog logger
            )
        {
            bool retValue = false;
            if (self.IsSystemObject)
                return true;

            if (inMemDatabase.Views.Contains(self.Name, self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }


            logger.Log("VIEW", self.FName());
            View newsp = new View(inMemDatabase, self.Name, self.Schema);
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
                

                error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                    .Select(ex1 => ex1.Message));



            }
            newsp = null;
            return retValue;

        }
    }
}

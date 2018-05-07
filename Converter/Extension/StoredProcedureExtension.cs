using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Converter.Extension
{
    public static class StoredProcedureExtension
    {

        public static string FName(this StoredProcedure self)
        {
            return "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";
        }

        public static bool SwitchToMo(this StoredProcedure self, Database InMemDatabase, Database Traditional, Configuration.Configuration cnf, ref string error, ILog logger)
        {
            bool retValue = false;
            if (self.IsSystemObject)
                return true;

            if (InMemDatabase.StoredProcedures.Contains(self.Name, self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }


            logger.Log("SP", self.FName());
            StoredProcedure newsp = new StoredProcedure(InMemDatabase, self.Name, self.Schema);
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

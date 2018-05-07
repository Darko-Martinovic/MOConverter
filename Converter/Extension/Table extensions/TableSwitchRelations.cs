using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Extension
{
    public static partial class TableExtension
    {


        public static bool SwitchRelationsToMo(this Table self, Database InMemDatabase, Database Traditional, Configuration.Configuration cnf, ref string error, ILog logger)
        {
            bool retValue = false;
            foreach (ForeignKey fk in self.ForeignKeys)
            {
                if (InMemDatabase.Tables[self.Name, self.Schema].ForeignKeys.Contains(fk.Name))
                {
                    logger.Log("\t" + "Already exists", fk.Name);
                    return true;
                }


                ForeignKey newFk = new ForeignKey(InMemDatabase.Tables[self.Name, self.Schema], fk.Name);
                newFk.CopyPropertiesFrom(fk);
                newFk.IsMemoryOptimized = true;
                foreach (ForeignKeyColumn fkc in fk.Columns)
                {
                    ForeignKeyColumn newfkc = new ForeignKeyColumn(newFk, fkc.Name, fkc.ReferencedColumn);
                    logger.Log("Relation", fk.Name);
                    newFk.Columns.Add(newfkc);
                }
                if (newFk.DeleteAction == ForeignKeyAction.Cascade)
                {
                    newFk.DeleteAction = ForeignKeyAction.NoAction;
                    logger.LogWarErr("Warning " + newFk.Name, " Delete action CASCADE is not supported " + self.FName());
                }
                if (newFk.UpdateAction == ForeignKeyAction.Cascade)
                {
                    newFk.UpdateAction = ForeignKeyAction.NoAction;
                    logger.LogWarErr("Warning " + newFk.Name, " Update action CASCADE is not supported " + self.FName());
                }
                try
                {
                    newFk.Create();
                }
                catch (Exception ex)
                {
                    error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                                       .Select(ex1 => ex1.Message));
                    logger.LogWarErr("Ralation Error", newFk.Name + " " + error);
                }


            }
            retValue = true;
            return retValue;

        }

    }
}
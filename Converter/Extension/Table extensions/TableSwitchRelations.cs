using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Extension
{
    public static partial class TableExtension
    {
        // back to traditional is not yet supported. It means it is always true. 
        private const bool V = true;

        public static bool SwitchRelationsToMo(
                                                this Table self,
                                                Database inMemDatabase, 
                                                ref string error,
                                                ILog logger
                                                )
        {

            Table inMemTable = inMemDatabase.Tables[self.Name, self.Schema];
            if (inMemTable == null)
            {
                error = "\tIn-memory table " + self.Schema + "." + self.Name + " does not exists";
                return false;
            }

            foreach (ForeignKey fk in self.ForeignKeys)
            {
                if (inMemTable.ForeignKeys.Contains(fk.Name))
                {
                    logger.Log("\t" + "Already exists", fk.Name);
                }
                else
                {
                    var newFk = new ForeignKey(inMemDatabase.Tables[self.Name, self.Schema], fk.Name);
                    newFk.CopyPropertiesFrom(fk);
                    newFk.IsMemoryOptimized = V;
                    foreach (ForeignKeyColumn fkc in fk.Columns)
                    {
                        var newfkc = new ForeignKeyColumn(newFk, fkc.Name, fkc.ReferencedColumn);
                        logger.Log("Relation", fk.Name);
                        newFk.Columns.Add(newfkc);
                    }
                    if (newFk.DeleteAction == ForeignKeyAction.Cascade)
                    {
                        newFk.DeleteAction = ForeignKeyAction.NoAction;
                        logger.LogWarErr($"Warning {newFk.Name}",
                            $" Delete action CASCADE is not supported {self.FName()}");
                    }
                    if (newFk.DeleteAction == ForeignKeyAction.SetNull)
                    {
                        newFk.DeleteAction = ForeignKeyAction.NoAction;
                        logger.LogWarErr($"Warning  {newFk.Name}",
                            $" Delete action SET NULL is not supported {self.FName()}");
                    }
                    if (newFk.UpdateAction == ForeignKeyAction.Cascade)
                    {
                        newFk.UpdateAction = ForeignKeyAction.NoAction;
                        logger.LogWarErr($"Warning  {newFk.Name}",
                            $" Update action CASCADE is not supported {self.FName()}");
                    }

                    try
                    {
                        newFk.Create();
                    }
                    catch (Exception ex)
                    {
                        error = string.Join($"{Environment.NewLine}\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                                           .Select(ex1 => ex1.Message));
                        logger.LogWarErr("Ralation Error", $"{newFk.Name} {error}");
                    }
                }
            }

            return error == "";

        }
        

    }
}
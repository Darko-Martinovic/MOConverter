using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Diagnostics;
using System.Linq;

namespace Converter.Extension
{
    public static class UserDefinedTableTypesExtension
    {
        public static string FName(this UserDefinedTableType self)
        {
            return "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";
        }
        public static bool SwitchToMo(this UserDefinedTableType self,
                                      Database InMemDatabase, 
                                      Database Traditional, 
                                      Configuration.Configuration cnf, 
                                      ref string error, 
                                      ILog logger)
        {
            bool retvalue = false;


            bool hasPrimaryKey = false;

            if (InMemDatabase.UserDefinedTableTypes.Contains(self.Name,self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }





            bool hasIdentities = false;

            UserDefinedTableType newTable = new UserDefinedTableType(InMemDatabase, self.Name, self.Schema);

            foreach (Column c in self.Columns)
            {
                Column newColumn = new Column(newTable, c.Name);
                //newColumn.Default = c.Default;
                newColumn.CopyPropertiesFrom(c);

                TableExtension.SupportUnsupported(newColumn, c, Traditional, logger, ref error, ref hasIdentities,false);


                newTable.Columns.Add(newColumn);

            }

            foreach (Index i in self.Indexes)
            {
                if (i.IndexKeyType == IndexKeyType.DriPrimaryKey)
                {
                    Index idx = new Index(newTable, i.Name);
                    idx.IndexType = IndexType.NonClusteredIndex;
                    idx.IndexKeyType = IndexKeyType.DriPrimaryKey;
                    foreach (IndexedColumn ic in i.IndexedColumns)
                    {
                        idx.IndexedColumns.Add(new IndexedColumn(idx, ic.Name));
                    }
                    newTable.Indexes.Add(idx);
                    hasPrimaryKey = true;
                    break;
                }
            }

            newTable.IsMemoryOptimized = true;
            if (hasPrimaryKey == false)
            {
                Column pkColumn = new Column(newTable, "pk" + newTable.Name);
                pkColumn.DataType.SqlDataType = SqlDataType.Int;
                pkColumn.Identity = true;
                pkColumn.IdentitySeed = 1;
                pkColumn.IdentityIncrement = 1;
                pkColumn.Nullable = false;
                newTable.Columns.Add(pkColumn);
                Index idx = new Index(newTable, pkColumn.Name);
                idx.IndexType = IndexType.NonClusteredIndex;
                idx.IndexKeyType = IndexKeyType.DriPrimaryKey;
                idx.IndexedColumns.Add(new IndexedColumn(idx, pkColumn.Name));
                newTable.Indexes.Add(idx);
                hasPrimaryKey = true;
            }
            try
            {
                logger.Log("Create UDT ", newTable.FName());
                newTable.Create();
                retvalue = true;
            }
            catch (Exception ex)
            {
                //if (Debugger.IsAttached)
                //    Debugger.Break();
                

                error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                    .Select(ex1 => ex1.Message));

                return false;
            }
            newTable = null;
            return retvalue;
        }
    }
}

using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Extension
{
    public static class UserDefinedTableTypesExtension
    {
        internal static string FName(this UserDefinedTableType self) => "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";
        public static bool SwitchToMo(this UserDefinedTableType self,
                                      Database inMemDatabase, 
                                      Database traditional, 
                                      Configuration.Configuration cnf, 
                                      ref string error, 
                                      ILog logger)
        {



            bool hasPrimaryKey = false;

            if (inMemDatabase.UserDefinedTableTypes.Contains(self.Name,self.Schema))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }


            var hasIdentities = false;

            UserDefinedTableType newTable = new UserDefinedTableType(inMemDatabase, self.Name, self.Schema);

            foreach (Column c in self.Columns)
            {

                new Column(newTable, c.Name).CopyPropertiesFrom(c);

                TableExtension.SupportUnsupported(new Column(newTable, c.Name), c, traditional, logger, ref error, ref hasIdentities,
                    false);


                newTable.Columns.Add(new Column(parent: newTable, name: c.Name));

            }

            foreach (Index i in self.Indexes)
            {
                if (i.IndexKeyType != IndexKeyType.DriPrimaryKey) continue;

                Index idx = new Index(newTable, i.Name)
                {
                    IndexType = IndexType.NonClusteredIndex,
                    IndexKeyType = IndexKeyType.DriPrimaryKey
                };
                foreach (IndexedColumn ic in i.IndexedColumns)
                    idx.IndexedColumns.Add(new IndexedColumn(idx, ic.Name));
                newTable.Indexes.Add(idx);
                hasPrimaryKey = true;
                break;
            }

            newTable.IsMemoryOptimized = true;
            if (hasPrimaryKey == false)
            {
                Column pkColumn =
                    new Column(newTable, "pk" + newTable.Name)
                    {
                        DataType = {SqlDataType = SqlDataType.Int},
                        Identity = true,
                        IdentitySeed = 1,
                        IdentityIncrement = 1,
                        Nullable = false
                    };
                newTable.Columns.Add(column: pkColumn);
                var idx = new Index(newTable, pkColumn.Name)
                {
                    IndexType = IndexType.NonClusteredIndex,
                    IndexKeyType = IndexKeyType.DriPrimaryKey
                };
                idx.IndexedColumns.Add(new IndexedColumn(idx, pkColumn.Name));
                newTable.Indexes.Add(idx);
                hasPrimaryKey = true;
            }
            try
            {
                logger.Log("Create UDT ", newTable.FName());
                newTable.Create();
                return true;
            }
            catch (Exception ex)
            {
                //if (Debugger.IsAttached)
                //    Debugger.Break();
                

                error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                    .Select(ex1 => ex1.Message));

                return false;
            }
        }
    }
}

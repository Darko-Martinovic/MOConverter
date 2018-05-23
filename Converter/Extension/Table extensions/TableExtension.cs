using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Diagnostics;
using System.Linq;
using Converter.Enums;
using static Converter.Options.Options;

namespace Converter.Extension
{
    public static partial class TableExtension
    {


        public static bool SwitchToMo(
                                      this Table self, 
                                      Database inMemDatabase, 
                                      Database traditional, 
                                      Configuration.Configuration cnf, 
                                      Options.Options o, 
                                      ref string error, 
                                      ILog logger,
                                      SqlServerMoFeatures enumFeatures)
        {
            bool retValue = false;
            string schemaName = self.Schema;
            string dataBaseName = self.Parent.Name;

            if (inMemDatabase.Schemas.Contains(schemaName) == false)
            {
                Schema hr = new Schema(inMemDatabase, schemaName);
                inMemDatabase.Schemas.Add(hr);
                inMemDatabase.Schemas[schemaName].Create();
            }

            if (inMemDatabase.Tables.Contains(self.Name, schemaName))
            {
                logger.Log("\t" + "Already exists", self.FName());
                return true;
            }


            bool hasIdentities = false;


            Table newTable = new Table(inMemDatabase, self.Name, schemaName)
            {
                // default true
                IsMemoryOptimized = true,
                // default schema and data
                Durability = DurabilityType.SchemaAndData
            };


            // Add columns
            foreach (Column c in self.Columns)
            {
                var newColumn = new Column(newTable, c.Name);

                newColumn.CopyPropertiesFrom(c);

                SupportUnsupported(newColumn, c, traditional, logger, ref error, ref hasIdentities,true);

                newTable.Columns.Add(newColumn);

            }


            //Add indexes
            bool hasPrimaryKey = false;
            foreach (Index i in self.Indexes)
            {
                if (i.IndexKeyType == IndexKeyType.DriPrimaryKey)
                {
                    hasPrimaryKey = true;
                    Index idx = new Index(newTable, i.Name);
                    if (o.UseHashIndexes == IndexDecision.Hash)
                    {
                        idx.IndexType = IndexType.NonClusteredHashIndex;
                        idx.BucketCount = self.RowCount == 0 ? 64 : (int)self.RowCount *2;
                    }
                    else if (o.UseHashIndexes == IndexDecision.Range)
                        idx.IndexType = IndexType.NonClusteredIndex;
                    else
                    {
                        if (self.ExtendedProperties[cnf.EpName] != null)
                        {
                            var value = self.ExtendedProperties[cnf.EpName].Value.ToString().ToUpper();
                            if (value == "HASH")
                            {
                                idx.IndexType = IndexType.NonClusteredHashIndex;
                                idx.BucketCount = self.RowCount == 0 ? 64 : (int) self.RowCount;
                            }
                            else
                            {
                                idx.IndexType = IndexType.NonClusteredIndex;
                            }
                        }
                        else
                        {
                            idx.IndexType = IndexType.NonClusteredIndex;
                        }
                    }

                    idx.IndexKeyType = IndexKeyType.DriPrimaryKey;
                    foreach (IndexedColumn ic in i.IndexedColumns)
                        idx.IndexedColumns.Add(new IndexedColumn(idx, ic.Name));
                    newTable.Indexes.Add(idx);
                }
            }

            int noIndexes = hasPrimaryKey ? 1 : 0;
            foreach (Index i in self.Indexes)
            {
                if (i.IndexKeyType == IndexKeyType.DriPrimaryKey)
                    continue;

                if (i.IndexType == IndexType.NonClusteredIndex)
                {

                    // Limit the total number of indexes to 8 for SQLServer2016
                    if (enumFeatures == SqlServerMoFeatures.SqlServer2016 && noIndexes == 8)
                    {
                        logger.LogWarErr("Error:Create index failed", $"Could not create in-memory index {i.Name} " +
                                                                      $"because it exceeds the maximum of 8 allowed per table or view.");
                        continue;
                    }

                    Index idx = new Index(newTable, i.Name)
                    {
                        IndexType = IndexType.NonClusteredIndex,
                        IndexKeyType = IndexKeyType.None
                    };

                    bool hasColumns = false;
                    foreach (IndexedColumn ic in i.IndexedColumns)
                    {
                        if (ic.IsIncluded)
                            continue;
                        // nvarchar(max) is not allowed
                        if (newTable.Columns[ic.Name].DataType.MaximumLength == -1)
                        {
                            logger.LogWarErr("Warning:Create index",
                                $"Could not include {ic.Name} in index {idx.Name} The column has nvarchar(max) type which is not allowed!");
                            continue;
                        }
                        idx.IndexedColumns.Add(new IndexedColumn(idx, ic.Name));
                        hasColumns = true;
                    }
                    if (hasColumns)
                    {
                        newTable.Indexes.Add(idx);
                        noIndexes++;

                    }
                }
            }


            if (hasPrimaryKey == false)
            {
                error = $"Error:Table :{self.FName()} has no primary key";
                //logger.LogWarErr(error, self.FName());
                return false;
            }

            //if (inMemDatabase.Tables.Contains(newTable.Name, schemaName))
            //    inMemDatabase.Tables[newTable.Name, schemaName].Drop();

            // Add checks
            foreach (Check ch in self.Checks)
            {
                Check newch = new Check(newTable, ch.Name);
                newch.CopyPropertiesFrom(ch);
                if (newch.Text.ToLower().Contains("upper") || newch.Text.ToLower().Contains("like") ||
                    newch.Text.ToLower().Contains("charindex"))
                {
                    logger.LogWarErr($"Warning {newch.Name}",
                        $" can not apply constraint on table {self.FName()} because it contains forbidden functions ");
                    continue;
                }
                newTable.Checks.Add(newch);
            }

            // Skip triggers
            foreach (Trigger tr in self.Triggers)
            {
                logger.LogWarErr($"Warning {tr.Name}", $" can not create trigger on table {self.FName()}.Please, create trigger manually! ");
            }

            try
            {
                logger.Log("Create table ", newTable.FName());
                newTable.Create();
            }
            catch (Exception ex)
            {
               
                error = string.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                    .Select(ex1 => ex1.Message));

                if (Debugger.IsAttached)
                    Debugger.Break();

                return false;
            }


            // if copy data is checked
            if (o.CopyData)
            {
                if (inMemDatabase.Tables.Contains(cnf.HelperTableName, cnf.HelperSchema))
                    inMemDatabase.Tables[cnf.HelperTableName, cnf.HelperSchema].Drop();

                try
                {
                    logger.Log("Insert data ", newTable.FName());
                    //Insert into 
                    traditional.ExecuteNonQuery(self.InsertIntoStm(inMemDatabase.Name, cnf.FullName));
                    //Insert statement
                    Table test = inMemDatabase.Tables[cnf.HelperTableName, cnf.HelperSchema];
                    inMemDatabase.ExecuteNonQuery(newTable.FullInsertStm(test.SelectStm(), hasIdentities, cnf.FullName));
                    retValue = true;
                    logger.Log("OK ", newTable.FName());
                    //
                }
                catch (Exception ex)
                {
                    if (inMemDatabase.Tables.Contains(newTable.Name, schemaName))
                        inMemDatabase.Tables[newTable.Name, schemaName].Drop();

                    logger.Log("Error", self.FName());
                

                    error = string.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                        .Select(ex1 => ex1.Message));

                    if (Debugger.IsAttached)
                        Debugger.Break();


                    return false;
                }
            }


            newTable = null;

            return retValue;
        }

        public static void SupportUnsupported(Column newColumn, Column c, Database traditional, ILog logger, ref string error, ref bool hasIdentities, bool isTable)
        {
            if (c.DataType.SqlDataType == SqlDataType.UserDefinedDataType)
            {
                UserDefinedDataType ud = traditional.UserDefinedDataTypes[c.DataType.Name, c.DataType.Schema];
                try
                {
                    newColumn.DataType = new DataType
                    {
                        SqlDataType = Uddt.Type.DetermineSqlDataType(ud.SystemType, ref error),
                        MaximumLength = c.DataType.MaximumLength,
                        NumericScale = c.DataType.NumericScale,
                        NumericPrecision = c.DataType.NumericPrecision
                    };
                    newColumn.Nullable = c.Nullable;
                    newColumn.Default = c.Default;
                    logger.LogWarErr("Warning " + (isTable == true ? c.FName() : c.UdtName()),
                                      $"Convertion datatype : {ud.Name}  to  {newColumn.DataType.SqlDataType.ToString()}");
                }
                catch (Exception ex)
                {
                    //if (Debugger.IsAttached)
                    //    Debugger.Break();


                    error = string.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                        .Select(ex1 => ex1.Message));
                    logger.LogWarErr("COLUMN:Error", error);
                    return;
                }
            }
            // support for CLR types
            else if (c.DataType.SqlDataType == SqlDataType.HierarchyId ||
                     c.DataType.SqlDataType == SqlDataType.Geography || c.DataType.SqlDataType == SqlDataType.Geometry)
            {
                newColumn.DataType.SqlDataType = SqlDataType.NVarChar;
                if (c.DataType.SqlDataType == SqlDataType.HierarchyId)
                    newColumn.DataType.MaximumLength = 1000;
                else
                    newColumn.DataType.MaximumLength = -1;

                logger.LogWarErr("Warning " + (isTable ? c.FName() : c.UdtName()),
                    $"Convertion CLR datatype to {newColumn.DataType.SqlDataType.ToString()}");
            }
            // support for XML type
            else if (c.DataType.SqlDataType == SqlDataType.Xml)
            {
                newColumn.DataType.SqlDataType = SqlDataType.NVarChar;
                newColumn.DataType.MaximumLength = -1;
                logger.LogWarErr("Warning " + (isTable ? c.FName() : c.UdtName()),
                                 $"Convertion XML datatype to  {newColumn.DataType.SqlDataType.ToString()}");
            }
            else if (c.DataType.SqlDataType == SqlDataType.Variant || c.DataType.SqlDataType == SqlDataType.Text ||
                     c.DataType.SqlDataType == SqlDataType.NText)
            {
                newColumn.DataType.SqlDataType = SqlDataType.NVarChar;
                newColumn.DataType.MaximumLength = -1;
                logger.LogWarErr("Warning " + (isTable  ? c.FName() : c.UdtName()),
                                 "Convertion " + c.DataType.SqlDataType + " TO " + newColumn.DataType.SqlDataType);
            }
            else if (c.DataType.SqlDataType == SqlDataType.Image)
            {
                newColumn.DataType.SqlDataType = SqlDataType.VarBinaryMax;
                newColumn.DataType.MaximumLength = -1;
                logger.LogWarErr("Warning " + (isTable ? c.FName() : c.UdtName()),
                                 "Convertion " + c.DataType.SqlDataType + " TO " + newColumn.DataType.SqlDataType);
            }
            else if (c.DataType.SqlDataType == SqlDataType.DateTimeOffset)
            {
                newColumn.DataType.SqlDataType = SqlDataType.DateTime2;
                logger.LogWarErr("Warning " + (isTable  ? c.FName() : c.UdtName()),
                                 "Convertion " + c.DataType.SqlDataType + " TO " + newColumn.DataType.SqlDataType);
            }
            //else
            //{
            //newColumn.DataType = c.DataType;
            //}

            if (c.Computed)
            {
                newColumn.Computed = false;
                newColumn.ComputedText = String.Empty;
                //newColumn.IsPersisted = c.IsPersisted;
                logger.LogWarErr("Warning " + (isTable ? c.FName() : c.UdtName()),
                                 $"Can not apply computed column {c.ComputedText}");
            }

            //newColumn.Collation = c.Collation;
            //newColumn.Identity = c.Identity;
            if (c.Identity)
            {

                hasIdentities = true;
                // has to be 1
                if (c.IdentityIncrement != 1 || c.IdentitySeed != 1)
                    logger.LogWarErr("Warning " + (isTable ? newColumn.FName() : newColumn.UdtName()),
                                    " setting identity seed and identity increment to 1 ");
                newColumn.IdentityIncrement = 1;
                newColumn.IdentitySeed = 1;


            }


        }



    }
}

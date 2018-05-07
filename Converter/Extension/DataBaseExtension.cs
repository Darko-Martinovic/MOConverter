using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;

namespace Converter.Extension
{
    public static class DataBaseExtension
    {

        
        public static bool SwichToMo( this Database self, 
                                        Database dbInMemory,
                                        ILog logger,
                                        Configuration.Configuration cnf,
                                        Options.Options o,
                                        SQLServerMoFeatures enumFeatures)
        {
            bool retvalue = false;




            logger.SetOverall("1/6");
            logger.CurrentItem = 1;
            logger.Counter = self.Tables.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);
            TableCollection tables = self.Tables;

            foreach (Table tbl in tables )
            {


                if (o.SchemaContains.Equals(string.Empty) == false && tbl.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.TableContains.Equals(string.Empty) == false && tbl.Name.Contains(o.TableContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                //cnnInMem.Connect();
                //serverInMem = new Server(cnnInMem);
                //dbInMemory = serverInMem.Databases[i.inMemoryDataBaseName];

                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;
                if (tbl.SwitchToMo(dbInMemory, self, cnf, o, ref error, logger,enumFeatures) == false)
                {
                    logger.LogWarErr("TABLE:Error ", error);
                }
                //Thread.Sleep(50);

                logger.CurrentItem++;
                //cnnInMem.Disconnect();

            }

            logger.SetOverall("2/6");
            //switch relations
            logger.CurrentItem = 1;
            logger.Counter = tables.Count;
            logger.SetMaxValue(logger.Counter);



            foreach (Table tbl in tables)
            {

                if (o.SchemaContains.Equals(string.Empty) == false && tbl.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.TableContains.Equals(string.Empty) == false && tbl.Name.Contains(o.TableContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;
                if (tbl.SwitchRelationsToMo(dbInMemory, self, cnf, ref error, logger) == false)
                {
                    logger.LogWarErr("RELATION:Error", error);
                }

                logger.CurrentItem++;

            }
            

            //3/6
            logger.SetOverall("3/6");
            //switch relations
            logger.CurrentItem = 1;
            logger.Counter = self.UserDefinedTableTypes.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            UserDefinedTableTypeCollection udtts = self.UserDefinedTableTypes;

            foreach (UserDefinedTableType tbl in udtts)
            {

                if (o.SchemaContains.Equals(string.Empty) == false && tbl.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.TableContains.Equals(string.Empty) == false && tbl.Name.Contains(o.TableContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;
                if (tbl.SwitchToMo(dbInMemory, self, cnf, ref error, logger) == false)
                {
                    logger.LogWarErr("UDT:Error", error);
                }

                logger.CurrentItem++;

            }





            logger.SetOverall("4/6");
            // user defined function 
            logger.CurrentItem = 1;
            logger.Counter = self.UserDefinedFunctions.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            UserDefinedFunctionCollection udfs = self.UserDefinedFunctions;

            foreach (UserDefinedFunction sp in udfs)
            {
                if (sp.IsSystemObject)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.SchemaContains.Equals(string.Empty) == false && sp.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }

                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;

                if (sp.SwitchToMo(dbInMemory, self, cnf, ref error, logger) == false)
                {
                    logger.LogWarErr("UDF:Error", error);
                }
                logger.CurrentItem++;

            }




            logger.SetOverall("5/6");
            //  STORED PROCEDURES
            logger.CurrentItem = 1;
            logger.Counter = self.StoredProcedures.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            StoredProcedureCollection sps = self.StoredProcedures;
            
            foreach (StoredProcedure sp in sps)
            {
                if (sp.IsSystemObject)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.SchemaContains.Equals(string.Empty) == false && sp.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }

                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;

                if (sp.SwitchToMo(dbInMemory, self, cnf, ref error, logger) == false)
                {
                    logger.LogWarErr("SP:Error", error);
                }
                logger.CurrentItem++;

            }



            logger.SetOverall("6/6");
            // VIEWS
            logger.CurrentItem = 1;
            logger.Counter = self.Views.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            ViewCollection vs = self.Views;
            
            foreach (Microsoft.SqlServer.Management.Smo.View sp in vs)
            {
                if (sp.IsSystemObject)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (o.SchemaContains.Equals(string.Empty) == false && sp.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }

                logger.SetValue(logger.CurrentItem);
                string error = string.Empty;

                if (sp.SwitchToMo(dbInMemory, self, cnf, ref error, logger) == false)
                {
                    logger.LogWarErr("VIEW:Error", error);
                }
                logger.CurrentItem++;

            }
            logger.SetOverall(string.Empty);
            logger.Log(string.Empty, string.Empty);
            retvalue = true;

            tables = null;
            udtts = null;
            vs = null;
            udfs = null;
            sps = null;

            return retvalue;
        }

    }
}

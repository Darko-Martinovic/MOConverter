using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Converter.Extension
{
    public static class DataBaseExtension
    {

        public static List<Table> ConvertToGenericTables(ICollection collection)
        {
            return collection.Cast<Table>().ToList();
        }
        public static List<StoredProcedure> ConvertToGenericStoredProcedures(ICollection collection)
        {
            return collection.Cast<StoredProcedure>().ToList();
        }
        public static List<UserDefinedFunction> ConvertToGenericFunctions(ICollection collection)
        {
            return collection.Cast<UserDefinedFunction>().ToList();
        }
        public static List<View> ConvertToGenericView(ICollection collection)
        {
            return collection.Cast<View>().ToList();
        }




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

            //List<Table> ilist = ConvertToGenericTables(self.Tables);
            //IEnumerable<Table> mylist = ilist.Where(a => a.Schema == "HumanResources");



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

            IEnumerable<UserDefinedFunction> udfs = ConvertToGenericFunctions(self.UserDefinedFunctions).Where(a => a.IsSystemObject == false);

            logger.CurrentItem = 1;
            logger.Counter = udfs.Count();


            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);


            foreach (UserDefinedFunction sp in udfs)
            {
                //if (sp.IsSystemObject)
                //{
                //    logger.CurrentItem++;
                //    logger.SetValue(logger.CurrentItem);
                //    continue;
                //}
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
            IEnumerable<StoredProcedure> sps = ConvertToGenericStoredProcedures(self.StoredProcedures).Where(a => a.IsSystemObject == false);

            logger.CurrentItem = 1;
            logger.Counter = sps.Count();

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            
            
            foreach (StoredProcedure sp in sps)
            {
                //if (sp.IsSystemObject)
                //{
                //    logger.CurrentItem++;
                //    logger.SetValue(logger.CurrentItem);
                //    continue;
                //}
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

            IEnumerable<View> vs = ConvertToGenericView(self.Views).Where(a => a.IsSystemObject == false);

            logger.CurrentItem = 1;
            logger.Counter = vs.Count();
            

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            //ViewCollection vs = self.Views;
            
            foreach (Microsoft.SqlServer.Management.Smo.View sp in vs)
            {
                //if (sp.IsSystemObject)
                //{
                //    logger.CurrentItem++;
                //    logger.SetValue(logger.CurrentItem);
                //    continue;
                //}
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

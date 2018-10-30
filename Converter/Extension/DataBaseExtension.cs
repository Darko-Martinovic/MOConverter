using Converter.Enums;
using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;

namespace Converter.Extension
{
    public static class DataBaseExtension
    {



        /// <summary>
        /// 
        /// </summary>
        /// <param name="self"></param>
        /// <param name="dbInMemory"></param>
        /// <param name="logger"></param>
        /// <param name="cnf"></param>
        /// <param name="o"></param>
        /// <param name="enumFeatures"></param>
        /// <returns></returns>
        public static bool SwitchToMo( this Database self, 
                                        Database dbInMemory,
                                        ILog logger,
                                        Configuration.Configuration cnf,
                                        Options.Options o,
                                        SqlServerMoFeatures enumFeatures)
        {


            logger.SetOverall("1/6");
            logger.CurrentItem = 1;
            logger.Counter = self.Tables.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);
            var tables = self.Tables;




            foreach (Table tbl in tables )
            {


                if (o.SchemaContains.Equals(string.Empty) == false && tbl.Schema.Contains(o.SchemaContains) == false)
                {
                    logger.CurrentItem++;
                    logger.SetValue(logger.CurrentItem);
                    continue;
                }
                if (string.Empty.Equals(o.TableContains) == false && tbl.Name.Contains(o.TableContains) == false)
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
                if (tbl.SwitchToMo(dbInMemory, self, cnf, o, ref error, logger, enumFeatures) == false)
                    logger.LogWarErr("TABLE:Error ", error);
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
                var error = string.Empty;
                if (tbl.SwitchRelationsToMo(dbInMemory, ref error, logger) == false)
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

            var udtts = self.UserDefinedTableTypes;

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
                var error = string.Empty;
                if (tbl.SwitchToMo(dbInMemory, self, ref error, logger) == false)
                {
                    logger.LogWarErr("UDT:Error", error);
                }

                logger.CurrentItem++;

            }





            logger.SetOverall("4/6");
            // user defined function 

            //IEnumerable<UserDefinedFunction> udfs = ConvertToGenericList<UserDefinedFunction>(self.UserDefinedFunctions).Where(a => a.IsSystemObject == false);
            var udfs = self.UserDefinedFunctions;

            logger.CurrentItem = 1;
            logger.Counter = udfs.Count;


            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);


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

                if (sp.SwitchToMo(dbInMemory, ref error, logger) == false)
                    logger.LogWarErr("UDF:Error", error);
                logger.CurrentItem++;

            }




            logger.SetOverall("5/6");
            //  STORED PROCEDURES
            //IEnumerable<StoredProcedure> sps = ConvertToGenericList<StoredProcedure>(self.StoredProcedures).Where(a => a.IsSystemObject == false);
            var sps = self.StoredProcedures;

            logger.CurrentItem = 1;
            logger.Counter = sps.Count;

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            
            
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

                if (sp.SwitchToMo(dbInMemory, ref error, logger) == false)
                {
                    logger.LogWarErr("SP:Error", error);
                }
                logger.CurrentItem++;

            }



            logger.SetOverall("6/6");
            var vs = self.Views;
            // VIEWS

            //IEnumerable<View> vs = ConvertToGenericList<View>(self.Views).Where(a => a.IsSystemObject == false);

            logger.CurrentItem = 1;
            logger.Counter = vs.Count;
            

            logger.SetValue(logger.CurrentItem);
            logger.SetMaxValue(logger.Counter);

            
            
            foreach (View sp in vs)
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

                if (sp.SwitchToMo(dbInMemory, ref error, logger) == false)
                {
                    logger.LogWarErr("VIEW:Error", error);
                }
                logger.CurrentItem++;

            }
            logger.SetOverall(string.Empty);
            logger.Log(string.Empty, string.Empty);
            

            tables = null;
            udtts = null;
            vs = null;
            udfs = null;
            sps = null;

            return true;
        }

    }
    //public static List<T> ConvertToGenericList<T>(ICollection collection)
    //{
    //    return collection.Cast<T>().ToList();
    //}

}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Converter.DataAccess
{
    public static class DataAccess
    {
        public static DataSet GetDataSet(
                                         string connectionString, 
                                         string commandText, 
                                         List<SqlParameter> listOfParams, 
                                         out string error
            )
        {
            var ds = new DataSet();
            error = string.Empty;
            try
            {
                using (var cnn = new SqlConnection(connectionString))
                {

                    using (var command = new SqlCommand(commandText, cnn))
                    {
                        cnn.Open();
                        if (listOfParams != null)
                        {
                            foreach (var p in listOfParams)
                            {
                                command.Parameters.Add(p);
                            }
                        }
                        using (var sqlAdp = new SqlDataAdapter())
                        {
                            sqlAdp.SelectCommand = command;
                            sqlAdp.Fill(ds);
                        }
                        cnn.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            return ds;
        }
        public static string GetConnectionString(string serverName, string dataBaseName, bool isWindowsAuth, string userName, string password)
        {
            string connectionString =
                $"Data Source={serverName};Integrated Security=SSPI;Initial Catalog={dataBaseName}";
            if (isWindowsAuth == false)
                connectionString =
                    $"Data Source={serverName};User Id={userName}; password= {password};Initial Catalog={dataBaseName}";
            return connectionString;
        }
       
       
        public static object ExecuteScalar(string connectionString, string query)
        {
            object retvalue;
            try
            {
                using (var cnn = new SqlConnection(connectionString))
                {

                    using (var command = new SqlCommand(query, cnn))
                    {
                        cnn.Open();
                        retvalue = command.ExecuteScalar();
                        cnn.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                retvalue = ex.Message;
            }
            return retvalue;
        }

   


    }
}

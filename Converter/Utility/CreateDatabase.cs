using Converter.Extension;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Linq;

namespace Converter.Utility
{
    public static class CreateDatabase
    {

        public static bool Create(
                                   Server srv, 
                                   string databaseName, 
                                   ref string error,
                                   string fileGroupName, 
                                   string fileName,
                                   string path
            )
        {
            var retValue = false;
            var exist = srv.Databases[databaseName] != null;

            if (exist == false)
            {
                var dbnew = new Database(srv, databaseName)
                {
                    // ON EXPRESS EDITION could be true which on the other hand results with failure in database creation
                    AutoClose = false,
                    RecoveryModel = RecoveryModel.Simple
                };


                srv.Databases.Add(dbnew);
                try
                {
                    srv.Databases[databaseName].Create();
                    srv.Refresh();
                   
                    dbnew.ExecuteNonQuery($"ALTER DATABASE {dbnew.Name}     " +
                                          " SET MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT = ON; ");

                    if (dbnew.CompatibilityLevel < CompatibilityLevel.Version130)
                    {
                        dbnew.CompatibilityLevel = CompatibilityLevel.Version130;
                        dbnew.Alter();
                        srv.Refresh();
                    }

                }
                catch (Exception ex)
                {
                    
                    error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                        .Select(ex1 => ex1.Message));

                    return false;
                }
            }

            var isMemoryOptimizedFileGropuExists = false;

            var db = srv.Databases[databaseName];

            foreach (FileGroup f in db.FileGroups)
            {
                if (f.FileGroupType == FileGroupType.MemoryOptimizedDataFileGroup)
                {
                    isMemoryOptimizedFileGropuExists = true;
                    break;
                }
            }
            if (isMemoryOptimizedFileGropuExists)
                retValue = true;
            else
            {
                if (db.FileGroups.Contains(fileGroupName) == false)
                {
                    // private const string C_FILE_GROUP = "mofg";
                    FileGroup mo = new FileGroup(db, fileGroupName, FileGroupType.MemoryOptimizedDataFileGroup);
                    db.FileGroups.Add(mo);
                    try
                    {
                        db.FileGroups[fileGroupName].Create();
                    }
                    catch (Exception ex)
                    {
                        
                        error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                            .Select(ex1 => ex1.Message));
                        return false;
                    }

                }
                // If the file for memory optimized file group does not exists - create 
                if (db.FileGroups[fileGroupName].Files.Contains(fileName) == false)
                {
                    // Create the file ( the container ) 
                    DataFile df = new DataFile(db.FileGroups[fileGroupName], fileName, path);
                    // Add the container to the memory optimized file group
                    db.FileGroups[fileGroupName].Files.Add(df);
                    // Actually create. Now it exists in the database
                    try
                    {
                        db.FileGroups[fileGroupName].Files[fileName].Create();
                        retValue = true;
                    }
                    catch (Exception ex)
                    {
                       
                        error = String.Join(Environment.NewLine + "\t", ex.CollectThemAll(ex1 => ex1.InnerException)
                            .Select(ex1 => ex1.Message));
                        return false;
                    }

                }

            }
            return retValue;
        }
    }
}

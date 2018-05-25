The largest database I ported to In-Memory OLTP by using this tool, was 15 GB in size and contains 1200 tables, 2200 indexes, and 2600 relations. The largest table was 600 MB in size. After converting, the table size was twice smaller.
The process of converting the whole database took 3 hours.

## :white_check_mark: To convert your database to In-Memory OLTP execute following code

```diff
-            // private Inputs i = null; the class that holds all inputs ( e.g. server name, type of authentication etc. )
-            var cnn = new ServerConnection(i.serverName);
-            cnn.Connect();
-            var server = new Server(cnn);
-            // The disk based database
-            var db = server.Databases[i.databaseName];
-            // Connect to the In-Memory Database
-            var cnnInMem = new ServerConnection(i.serverName);
-            cnnInMem.Connect();
-            var serverInMem = new Server(cnnInMem);
-            var dbInMemory = serverInMem.Databases[i.inMemoryDataBaseName];
-
-            // new features available starting with SQL Server 2017
-            var enumFeatures = SQLServerMoFeatures.SQLServer2016;
-            if (new Version(server.VersionString) >= new Version(C_NEW_FEATURES_VERSION))
-                enumFeatures = SQLServerMoFeatures.SQLServer2017;
-            // Switch to In-Memory 
-            success = db.SwichToMo(
-                                    dbInMemory,     // In-Memory database
-                                    (ILog)this,     // logger
-                                    cnf,            // configuration class
-                                    o,              // options
-                                    enumFeatures);
-
-     
```


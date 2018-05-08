## :white_check_mark: To convert your database to In-Memory OLTP execute following code

```diff
-            // private Inputs i = null; the class that holds all user inputs
-            ServerConnection cnn = new ServerConnection(i.serverName);
-            cnn.Connect();
-            Server server = new Server(cnn);
-            // The disk based database
-            Database db = server.Databases[i.databaseName];
-            // Connect to the In-Memory Database
-            ServerConnection cnnInMem = new ServerConnection(i.serverName);
-            cnnInMem.Connect();
-            Server serverInMem = new Server(cnnInMem);
-            Database dbInMemory = serverInMem.Databases[i.inMemoryDataBaseName];
-
-            // new features available starting with SQL Server 2017
-            SQLServerMoFeatures enumFeatures = SQLServerMoFeatures.SQLServer2016;
-            if (new Version(server.VersionString) >= new Version(C_NEW_FEATURES_VERSION))
-            {
-                enumFeatures = SQLServerMoFeatures.SQLServer2017;
-            }
-            // Switch to In-Memory 
-            success = db.SwichToMo(
-                                    dbInMemory,     // In-Memory database
-                                    (ILog)this,     // logger
-                                    cnf,            // configuration class
-                                    o,              // options
-                                    enumFeatures);
-
-      
-            cnnInMem.Disconnect();
-            cnn.Disconnect();
-            cnn = null;
-            cnnInMem = null;
-            server = null;
-            db = null;
-            serverInMem = null;
-            dbInMemory = null;
```


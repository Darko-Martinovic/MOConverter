-- Cases that need SQL Server 2022 or later
CREATE TABLE cases.Ledger (Id int PRIMARY KEY, Name nvarchar(10) NULL) WITH (SYSTEM_VERSIONING = ON, LEDGER = ON);

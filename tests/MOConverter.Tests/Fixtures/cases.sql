-- One table for each thing the assessment looks at, in the schema [cases]. AssessTests says
-- what each table's outcome must be. Run in a database with change tracking on.
CREATE SCHEMA cases;
GO
CREATE TYPE dbo.BigId FROM bigint NOT NULL;
CREATE TYPE dbo.Name50 FROM varchar(50) NULL;
CREATE TYPE dbo.Stamp FROM datetime2(3) NULL;
CREATE TYPE dbo.Moment FROM datetimeoffset(3) NULL;
CREATE SEQUENCE dbo.Numbers;
GO

-- Ready: the types, defaults, checks and computed columns memory-optimized tables accept
CREATE TABLE cases.Ready (
    Id int IDENTITY(1, 1) PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    Price decimal(10, 2) NULL CONSTRAINT CK_Ready_Price CHECK (Price >= 0 AND ROUND(Price, 0) < 1000000),
    Created datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Code char(3) NULL,
    Flag bit NOT NULL DEFAULT 0,
    Uid uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID(),
    Blob varbinary(max) NULL,
    Owner sysname NULL DEFAULT SUSER_SNAME(),
    Total AS Price * 2,
    INDEX IX_Ready_Name (Name)
);
INSERT cases.Ready (Name, Price) SELECT TOP (1000) CONCAT(N'Item ', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), 9.99 FROM sys.all_columns;

-- Ready: version 1 dropped this check, because "UpperLimit" contains "upper"
CREATE TABLE cases.UpperLimit (Id int PRIMARY KEY, UpperLimit int NULL CONSTRAINT CK_UpperLimit CHECK (UpperLimit > 0));

-- Ready: version 1 kept only 8 indexes, and dropped columnstore
CREATE TABLE cases.ManyIndexes (
    Id int PRIMARY KEY, A int, B int, C int, D int, E int, F int, G int, H int, I int,
    INDEX IX_A (A), INDEX IX_B (B), INDEX IX_C (C), INDEX IX_D (D), INDEX IX_E (E),
    INDEX IX_F (F), INDEX IX_G (G), INDEX IX_H (H), INDEX IX_I (I DESC)
);
CREATE TABLE cases.Columnstore (Id int NOT NULL PRIMARY KEY NONCLUSTERED, A int NULL, INDEX CCI_Columnstore CLUSTERED COLUMNSTORE);

-- Ready: names that need brackets
CREATE TABLE cases.[Order Details] ([Order Id] int NOT NULL PRIMARY KEY, [Select] int NULL, [Unit]]Price] money NULL);

-- Fixed: alias types get their base types, which version 1 got wrong (bigint became int,
-- varchar char, datetime2 datetime)
CREATE TABLE cases.AliasTypes (Id dbo.BigId PRIMARY KEY, Name dbo.Name50, Stamp dbo.Stamp);

-- Fixed: text, ntext and image
CREATE TABLE cases.OldLobTypes (Id int PRIMARY KEY, Notes text NULL, Wide ntext NULL, Picture image NULL);

-- Fixed: what indexes lose without changing results
CREATE TABLE cases.Indexes (
    Id int NOT NULL CONSTRAINT PK_Indexes PRIMARY KEY NONCLUSTERED,
    A int NULL, B int NULL, C int NULL,
    INDEX CX_Indexes_A CLUSTERED (A),
    INDEX IX_Indexes_B (B) INCLUDE (C),
    INDEX IX_Indexes_C (C) WHERE C IS NOT NULL
);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Indexes ON cases.Indexes (A, B);

-- Fixed: an index on a computed column needs it PERSISTED
CREATE TABLE cases.ComputedIndexed (Id int PRIMARY KEY, Price int NULL, Doubled AS Price * 2, INDEX IX_Doubled (Doubled));

-- Fixed: SPARSE, partitioning and compression
CREATE TABLE cases.Sparse (Id int PRIMARY KEY, Rare int SPARSE NULL);
CREATE PARTITION FUNCTION CasesByRange (int) AS RANGE RIGHT FOR VALUES (100);
CREATE PARTITION SCHEME CasesByRangeScheme AS PARTITION CasesByRange ALL TO ([PRIMARY]);
GO
CREATE TABLE cases.Partitioned (Id int PRIMARY KEY) ON CasesByRangeScheme (Id);
CREATE TABLE cases.Compressed (Id int PRIMARY KEY, Name nvarchar(50) NULL) WITH (DATA_COMPRESSION = PAGE);

-- Fixed: disabled objects aren't carried over
CREATE TABLE cases.Disabled (Id int PRIMARY KEY, A int NULL CONSTRAINT CK_Disabled_A CHECK (A LIKE '1%'), INDEX IX_Disabled_A (A));
ALTER TABLE cases.Disabled NOCHECK CONSTRAINT CK_Disabled_A;
ALTER INDEX IX_Disabled_A ON cases.Disabled DISABLE;

-- Lossy: types stored as other types
CREATE TABLE cases.LossyTypes (
    Id int PRIMARY KEY,
    Doc xml NULL,
    Place geography NULL,
    Shape geometry NULL,
    Node hierarchyid NULL,
    Moment datetimeoffset(3) NULL,
    Version rowversion
);
CREATE TABLE cases.AliasLossy (Id int PRIMARY KEY, Moment dbo.Moment);

-- Lossy: checks and defaults memory-optimized tables refuse
CREATE TABLE cases.Checks (
    Id int PRIMARY KEY,
    Code varchar(10) NULL CONSTRAINT CK_Checks_Code CHECK (Code LIKE 'A%'),
    Name varchar(10) NULL CONSTRAINT CK_Checks_Name CHECK (UPPER(Name) = Name)
);
CREATE TABLE cases.Defaults (Id int PRIMARY KEY, Num bigint NULL DEFAULT NEXT VALUE FOR dbo.Numbers, Host nvarchar(128) NULL DEFAULT HOST_NAME());

-- Lossy: IDENTITY other than (1,1), with a seed too big for .NET's decimal
CREATE TABLE cases.IdentitySeed (Id int IDENTITY(100, 5) PRIMARY KEY, Name nvarchar(10) NULL);
CREATE TABLE cases.BigSeed (Id decimal(38, 0) IDENTITY(1000000000000000000000000000000, 1) PRIMARY KEY);

-- Lossy: cascading foreign keys
CREATE TABLE cases.Parent (Id int PRIMARY KEY);
CREATE TABLE cases.Child (Id int PRIMARY KEY, ParentId int NULL CONSTRAINT FK_Child_Parent REFERENCES cases.Parent (Id) ON DELETE CASCADE ON UPDATE SET NULL);

-- Lossy: a trigger and change tracking
CREATE TABLE cases.Audited (Id int PRIMARY KEY, Name nvarchar(10) NULL);
GO
CREATE TRIGGER cases.TR_Audited ON cases.Audited AFTER INSERT AS SET NOCOUNT ON;
GO
CREATE TABLE cases.Tracked (Id int PRIMARY KEY);
ALTER TABLE cases.Tracked ENABLE CHANGE_TRACKING;

-- Blocked
CREATE TABLE cases.NoPrimaryKey (Id int NOT NULL, Name nvarchar(10) NULL);
CREATE TABLE cases.Variant (Id int PRIMARY KEY, Value sql_variant NULL);
CREATE TABLE cases.ComputedUpper (Id int PRIMARY KEY, Name varchar(10) NULL, Shout AS UPPER(Name));
CREATE TABLE cases.ComputedVariant (Id int PRIMARY KEY, Anything AS CONVERT(sql_variant, Id));
-- Blocked: a key to a unique constraint rather than the primary key, and the table it references
CREATE TABLE cases.UniqueParent (Id int PRIMARY KEY, Code int NOT NULL CONSTRAINT UQ_UniqueParent_Code UNIQUE);
CREATE TABLE cases.UniqueChild (Id int PRIMARY KEY, Code int NULL CONSTRAINT FK_UniqueChild_Code REFERENCES cases.UniqueParent (Code));
CREATE TABLE cases.UniqueFiltered (Id int PRIMARY KEY, Email varchar(50) NULL);
CREATE UNIQUE INDEX UX_UniqueFiltered_Email ON cases.UniqueFiltered (Email) WHERE Email IS NOT NULL;
CREATE TABLE cases.ColumnSet (Id int PRIMARY KEY, A int SPARSE NULL, Everything xml COLUMN_SET FOR ALL_SPARSE_COLUMNS);
CREATE TABLE cases.Person (Id int PRIMARY KEY, Name nvarchar(10) NULL) AS NODE;
CREATE TABLE cases.Temporal (
    Id int PRIMARY KEY,
    Name nvarchar(10) NULL,
    ValidFrom datetime2 GENERATED ALWAYS AS ROW START NOT NULL,
    ValidTo datetime2 GENERATED ALWAYS AS ROW END NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = cases.TemporalHistory));

-- Blocked: fine on its own, but linked to a table that can't be converted
CREATE TABLE cases.Linked (Id int PRIMARY KEY, VariantId int NULL CONSTRAINT FK_Linked_Variant REFERENCES cases.Variant (Id));
-- Blocked in turn: linked to Linked
CREATE TABLE cases.LinkedToLinked (Id int PRIMARY KEY, LinkedId int NULL CONSTRAINT FK_LinkedToLinked_Linked REFERENCES cases.Linked (Id));

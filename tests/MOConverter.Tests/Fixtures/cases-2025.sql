-- Cases that need SQL Server 2025 or later: its json and vector types
CREATE TABLE cases.NewTypes (Id int PRIMARY KEY, Document json NULL, Embedding vector(3) NULL);

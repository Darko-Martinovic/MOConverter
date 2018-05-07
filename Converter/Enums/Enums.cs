using System;

[Flags]
public enum SQLServerMoFeatures
{
    // Basic set of features
    SQLServer2016 = 0x01,
    // Support for computed columns, more than 8 indexes
    SQLServer2017 = 0x02,
}
using System;

namespace Converter.Enums
{
    [Flags]
    public enum SqlServerMoFeatures
    {
        // Basic set of features
        SqlServer2016 = 0x01,
        // Support for computed columns, more than 8 indexes
        SqlServer2017 = 0x02,
        //??
        SqlServer2019 = 0x03
    }
}
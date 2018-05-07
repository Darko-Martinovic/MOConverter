using Microsoft.SqlServer.Management.Smo;

namespace Converter.Extension
{
    public static class ColumnExtension
    {
        public static string QName(this Column self)
        {
            return "[" + self.Name + "]";
        }
        public static string FName(this Column self)
        {
            return ((Table)self.Parent).FName() + ".[" + self.Name + "]";
        }
        public static string UDTName(this Column self)
        {
            return ((UserDefinedTableType)self.Parent).FName() + ".[" + self.Name + "]";
        }


    }
}

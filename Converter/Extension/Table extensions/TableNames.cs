using Microsoft.SqlServer.Management.Smo;

namespace Converter.Extension
{
    public static partial class TableExtension
    {

        /// <summary>
        /// Quota table name
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string QName(this Table self)
        {
            return "[" + self.Name + "]";
        }


        /// <summary>
        /// Return full table name. It means database name + schema name + table name
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string FName(this Table self)
        {
            return "[" + self.Parent.Name + "].[" + self.Schema + "].[" + self.Name + "]";
        }

    }
}
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Text.RegularExpressions;

namespace Converter.Uddt
{
    public static class Type
    {
        public static SqlDataType DetermineSqlDataType(string input, ref string html)
        {
            SqlDataType retValue = SqlDataType.NVarChar;
            try
            {

                if (Regex.IsMatch(input.ToLower(), "nvarchar"))  //test it
                    retValue = SqlDataType.NVarChar;
                else if (Regex.IsMatch(input.ToLower(), "int"))  //test it 
                    retValue = SqlDataType.Int;
                else if (Regex.IsMatch(input.ToLower(), "char"))
                    retValue = SqlDataType.Char;
                else if (Regex.IsMatch(input.ToLower(), "decimal")) //test it 
                    retValue = SqlDataType.Decimal;
                else if (Regex.IsMatch(input.ToLower(), "datetime")) // test it
                    retValue = SqlDataType.DateTime;
                else if (Regex.IsMatch(input.ToLower(), "date"))
                    retValue = SqlDataType.Date;
                else if (Regex.IsMatch(input.ToLower(), "bit")) //test it
                    retValue = SqlDataType.Bit;
                else if (Regex.IsMatch(input.ToLower(), "bigint")) // test it
                    retValue = SqlDataType.BigInt;
                else if (Regex.IsMatch(input.ToLower(), "binary"))
                    retValue = SqlDataType.Binary;
                else if (Regex.IsMatch(input.ToLower(), "datetime2"))
                    retValue = SqlDataType.DateTime2;
                else if (Regex.IsMatch(input.ToLower(), "datetimeoffset"))
                    retValue = SqlDataType.DateTimeOffset;
                else if (Regex.IsMatch(input.ToLower(), "float")) // test it
                    retValue = SqlDataType.Float;
                else if (Regex.IsMatch(input.ToLower(), "image"))
                    retValue = SqlDataType.Image;
                else if (Regex.IsMatch(input.ToLower(), "money"))
                    retValue = SqlDataType.Money;
                else if (Regex.IsMatch(input.ToLower(), "nchar")) //test it
                    retValue = SqlDataType.NChar;
                else if (Regex.IsMatch(input.ToLower(), "ntext"))
                    retValue = SqlDataType.NText;
                else if (Regex.IsMatch(input.ToLower(), "real"))
                    retValue = SqlDataType.Real;
                else if (Regex.IsMatch(input.ToLower(), "smalldatetime"))
                    retValue = SqlDataType.SmallDateTime;
                else if (Regex.IsMatch(input.ToLower(), "smallint")) //test it
                    retValue = SqlDataType.SmallInt;
                else if (Regex.IsMatch(input.ToLower(), "smallmoney"))
                    retValue = SqlDataType.SmallMoney;
                else if (Regex.IsMatch(input.ToLower(), "text"))
                    retValue = SqlDataType.Text;
                else if (Regex.IsMatch(input.ToLower(), "time"))
                    retValue = SqlDataType.Time;
                else if (Regex.IsMatch(input.ToLower(), "timestamp"))
                    retValue = SqlDataType.Timestamp;
                else if (Regex.IsMatch(input.ToLower(), "tinyint"))
                    retValue = SqlDataType.TinyInt;
                else if (Regex.IsMatch(input.ToLower(), "uniqueidentifier")) // test it
                    retValue = SqlDataType.UniqueIdentifier;
                else if (Regex.IsMatch(input.ToLower(), "varbinary"))
                    retValue = SqlDataType.VarBinary;
                else if (Regex.IsMatch(input.ToLower(), "varchar"))
                    retValue = SqlDataType.VarChar;
                else if (Regex.IsMatch(input.ToLower(), "variant"))
                    retValue = SqlDataType.Variant;
                else if (Regex.IsMatch(input.ToLower(), "xml"))
                    retValue = SqlDataType.Xml;



            }
            catch (Exception ex)
            {
                html += ex.Message;
            }

            return retValue;
        }
    }
}

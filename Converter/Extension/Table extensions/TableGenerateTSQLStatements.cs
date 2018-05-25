using Microsoft.SqlServer.Management.Smo;
using System;
using System.Text;
using static Microsoft.SqlServer.Management.Smo.SqlDataType;


namespace Converter.Extension
{
    public static partial class TableExtension
    {

        /// <summary>
        /// Generate select statement 
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string SelectStm(this Table self)
        {
            var buildInto = new StringBuilder();
            var counter1 = 1;
            foreach (Column c in self.Columns)
            {
                switch (c.DataType.SqlDataType)
                {
                    case Xml:
                        buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(max) ) " + c.Name);
                        break;
                    case HierarchyId:
                    case Geography:
                    case Geometry:
                        buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(1000) ) " + c.Name);
                        break;
                    case Variant:
                    case Text:
                    case NText:
                        buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(max) ) " + c.Name);
                        break;
                    case Image:
                        buildInto.Append("CAST(" + c.QName() + " AS VARBINARY(max) ) " + c.Name);
                        break;
                    case SqlDataType.DateTimeOffset:
                        buildInto.Append("CAST(" + c.QName() + " AS DATETIME2 ) " + c.Name);
                        break;
                    default:
                        buildInto.Append(c.QName());
                        break;
                }

                if (counter1 < self.Columns.Count)
                {
                    buildInto.Append(",");
                }
                counter1++;
            }
            string retValue = buildInto.ToString();
            buildInto = null;
            return retValue;
        }

        public static string IdentityInsStm(this Table self, bool isOn) => "SET IDENTITY_INSERT " + self.FName() + (isOn ? " ON;" : " OFF");

        /// <summary>
        /// Build column list for insert
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string InsertStm(this Table self)
        {
            var insertColumn = new StringBuilder();
            int counter = 1;
            foreach (Column c in self.Columns)
            {
                insertColumn.Append(c.QName());
                if (counter < self.Columns.Count)
                    insertColumn.Append($",");
                counter++;
            }
            string retValue = insertColumn.ToString();
            insertColumn = null;
            return retValue;
        }


        public static string FullInsertStm(this Table self, string selectStm, bool hasIdentites, string fullName)
        {
            var sb = new StringBuilder();

            if (hasIdentites)
                sb.Append(self.IdentityInsStm(true)); //set identity insert

            sb.Append($" INSERT INTO  {self.FName()} ( {self.InsertStm() } )");
            sb.Append(Environment.NewLine);
            sb.Append($"SELECT {selectStm}  FROM   {fullName}");

            if (hasIdentites)
                sb.Append(self.IdentityInsStm(false)); //only one table per session has this value to ON

            var retValue = sb.ToString();
            sb = null;
            return retValue;
        }

        public static string InsertIntoStm(this Table self, string baseName, string fullName)
        {
            var sb = new StringBuilder();
            sb.Append($@" SELECT {self.SelectStm()} INTO {baseName}.{fullName} FROM {self.FName()}");
            var retValue = sb.ToString();
            sb = null;
            return retValue;
        }

    }
}
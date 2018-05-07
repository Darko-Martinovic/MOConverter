using Converter.Interface;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace Converter.Extension
{
    public static partial class TableExtension
    {

        /// <summary>
        /// Generate select statement 
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string selectStm(this Table self)
        {
            StringBuilder buildInto = new StringBuilder();
            int counter1 = 1;
            foreach (Column c in self.Columns)
            {
                if (c.DataType.SqlDataType == SqlDataType.Xml)
                {
                    buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(max) ) " + c.Name);
                }
                else if (c.DataType.SqlDataType == SqlDataType.HierarchyId || c.DataType.SqlDataType == SqlDataType.Geography || c.DataType.SqlDataType == SqlDataType.Geometry)
                {
                    buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(1000) ) " + c.Name);
                }
                else if (c.DataType.SqlDataType == SqlDataType.Variant || c.DataType.SqlDataType == SqlDataType.Text || c.DataType.SqlDataType == SqlDataType.NText)
                {
                    buildInto.Append("CAST(" + c.QName() + " AS NVARCHAR(max) ) " + c.Name);
                }
                else if (c.DataType.SqlDataType == SqlDataType.Image)
                {
                    buildInto.Append("CAST(" + c.QName() + " AS VARBINARY(max) ) " + c.Name);

                }
                else if (c.DataType.SqlDataType == SqlDataType.DateTimeOffset)
                {
                    buildInto.Append("CAST(" + c.QName() + " AS DATETIME2 ) " + c.Name);

                }
                else
                {
                    buildInto.Append(c.QName());
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

        /// <summary>
        /// Setup IDENTITY_INSERT STATEMENT
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string identityInsStm(this Table self, bool isOn)
        {
            return "SET IDENTITY_INSERT " + self.FName() + (isOn ? " ON;" : " OFF");
        }

        /// <summary>
        /// Build column list for insert
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string insertStm(this Table self)
        {
            StringBuilder insertColumn = new StringBuilder();
            int counter = 1;
            foreach (Column c in self.Columns)
            {
                insertColumn.Append(c.QName());
                if (counter < self.Columns.Count)
                {
                    insertColumn.Append(",");
                }
                counter++;
            }
            string retValue = insertColumn.ToString();
            insertColumn = null;
            return retValue;
        }


        public static string fullInsertStm(this Table self, string selectStm, bool hasIdentites, string fullName)
        {
            StringBuilder sb = new StringBuilder();

            if (hasIdentites)
                sb.Append(self.identityInsStm(true)); //set identity insert

            sb.Append(" INSERT INTO " + self.FName() + "(");
            sb.Append(self.insertStm());
            sb.Append(")");
            sb.Append(Environment.NewLine);
            sb.Append("SELECT ");

            sb.Append(selectStm);
            sb.Append(" FROM " + fullName);

            if (hasIdentites)
                sb.Append(self.identityInsStm(false)); //only one table per session has this value to ON

            string retValue = sb.ToString();
            return retValue;
        }

        public static string insertIntoStm(this Table self, string baseName, string fullName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(" SELECT " + self.selectStm() + " INTO " + baseName + "." + fullName + " FROM " + self.FName());
            string retValue = sb.ToString();
            return retValue;
        }

    }
}
using System.Text;

namespace Converter.Extension
{
    public static class StringExtension
    {
        public static string BracketObjectName(this string objectName)
        {
            var tempString = new StringBuilder(128);
            tempString.Append(@"[");
            tempString.Append(objectName.Replace(@"[", @"[[").Replace(@"]", @"]]"));
            tempString.Append(@"]");
            return tempString.ToString();
        }

    }
}

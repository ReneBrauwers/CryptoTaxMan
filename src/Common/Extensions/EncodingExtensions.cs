using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class EncodingExtensions
    {

        public static string ToHexString(this string str, int totalWidth = 40, char trailingChar = '0')
        {
            var sb = new StringBuilder();
            var bytes = Encoding.UTF8.GetBytes(str);
            sb.Append(Convert.ToHexString(bytes));

            string? returnValue;
            //foreach (var t in bytes)
            //{
            //    sb.Append(t.ToString("X2"));
            //}



            if (totalWidth == 0)
            {
                returnValue = sb.ToString(); // returns: "48656C6C6F20776F726C64" for "Hello world"
            }
            else
            {
                returnValue = sb.ToString().PadRight(totalWidth, trailingChar);
            }

            return returnValue;
        }

        public static string FromHexString(this string hexString, bool removeTrailingZeros)
        {
            string? returnValue;
            if (removeTrailingZeros)
            {
                returnValue = hexString.TrimEnd('0');
            }
            else
            {
                returnValue = hexString;
            }

            return Encoding.UTF8.GetString(Convert.FromHexString(returnValue));
        }

    }
}

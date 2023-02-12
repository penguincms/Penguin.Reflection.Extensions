using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Penguin.Reflection.Extensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public static class StringExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {




        /// <summary>
        /// Converts the string to a value that can safely be used as a variable name when writing code
        /// </summary>
        /// <param name="s">The string to sanitize</param>
        /// <returns>A value that can safely be used as a variable name when writing code</returns>
        public static string ToVariableName(this string s)
        {
            if (s is null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (char.IsDigit(s[0]))
            {
                s = "_" + s;
            }

            return string.Join("", s.AsEnumerable()
                                    .Select(chr => char.IsLetter(chr) || char.IsDigit(chr)
                                                   ? chr.ToString(CultureInfo.CurrentCulture)      // valid symbol
                                                   : "_" + (short)chr + "_") // numeric code for invalid symbol
                              );
        }
    }
}
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
        /// Attempts to convert a string to the specified type
        /// </summary>
        /// <typeparam name="T">The type to cast the return value as</typeparam>
        /// <param name="s">The string value</param>
        /// <param name="IgnoreCase">Whether or not case should be ignored (enum)</param>
        /// <returns>A casted representation of the string value</returns>
        public static T Convert<T>(this string s, bool IgnoreCase = false)
        {
            return (T)s.Convert(typeof(T), IgnoreCase);
        }

        private static bool IsValidEnumValue(string toCheck)
        {
            if (string.IsNullOrWhiteSpace(toCheck))
            {
                return false;
            }

            if (int.TryParse(toCheck, out _) || char.IsLetter(toCheck[0]))
            {
                return true;
            }

            return false;
        }

        private static IEnumerable<string> SplitEnumString(string toSplit)
        {
            string thisVal = string.Empty;
            for (int i = 0; i < toSplit.Length; i++)
            {
                char c = toSplit[i];

                if (char.IsLetter(c) || (char.IsDigit(c) && thisVal.Length > 0))
                {
                    thisVal += c;
                }
                else
                {
                    if (IsValidEnumValue(thisVal))
                    {
                        yield return thisVal;
                    }

                    thisVal = string.Empty;
                }
            }

            if (IsValidEnumValue(thisVal))
            {
                yield return thisVal;
            }
        }

        /// <summary>
        /// Converts a string to the requested type. Handles nullables.
        /// </summary>
        /// <param name="s">The string value</param>
        /// <param name="t">The type to cast the value as</param>
        /// <param name="IgnoreCase">Whether or not case should be ignored (enum)</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
        public static object Convert(this string s, Type t, bool IgnoreCase = false)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (t == typeof(string))
            {
                return s;
            }

            foreach (MethodInfo mi in t.GetMethods())
            {
                //Must be an explicit or implicit converter
                if (mi.Name != "op_Implicit" && mi.Name != "op_Explicit")
                {
                    continue;
                }

                //Of which the return type matches out target type
                if (mi.ReturnType != t)
                {
                    continue;
                }

                ParameterInfo[] parameters = mi.GetParameters();

                //It must have a single parameter
                if (parameters.Length != 1)
                {
                    continue;
                }

                //And that parameter must be a string
                if (parameters[0].ParameterType != typeof(string))
                {
                    continue;
                }

                //This is our matching conversion method, 
                //If we're still here.
                return mi.Invoke(null, new object[] { s });
            }

            if (t == typeof(bool))
            {
                if (s == "1")
                {
                    return true;
                }
                else if (s == "0")
                {
                    return false;
                }
                else
                {
                    return bool.Parse(s);
                }
            }

            //I feel like this could be done better by leveraging system types.
            if (string.IsNullOrWhiteSpace(s) && t.IsValueType)
            {
                if (t.IsValueType)
                {
                    return t.GetDefaultValue();
                }
                else
                {
                    return null;
                }
            }

            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (t.IsEnum)
            {
                StringComparison comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                if (char.IsDigit(s[0]))
                {
                    return Enum.Parse(t, s);
                }
                else
                {
                    object EnumValue = Enum.GetValues(t).Cast<object>().FirstOrDefault(e => string.Equals(s, e.ToString(), comparison));

                    if (EnumValue != null)
                    {
                        return EnumValue;
                    }
                    else
                    {
                        if (t.GetCustomAttribute<FlagsAttribute>() is null)
                        {
                            throw new Exception($"Enum value {s} not found on type {t}");
                        }
                        else
                        {
                            long value = 0;
                            Dictionary<string, long> EnumValues = new Dictionary<string, long>();

                            foreach (object val in Enum.GetValues(t))
                            {
                                object underlyingType = System.Convert.ChangeType(val, Enum.GetUnderlyingType(t));

                                EnumValues.Add(val.ToString(), System.Convert.ToInt64(underlyingType));
                            }

                            foreach (string toAdd in SplitEnumString(s))
                            {
                                value |= EnumValues[toAdd];
                            }

                            return Enum.ToObject(t, value);
                        }
                    }
                }
            }

            if (t == typeof(System.Guid))
            {
                return System.Guid.Parse(s);
            }

            if (Nullable.GetUnderlyingType(t) != null)
            {
                return Activator.CreateInstance(t, s.Convert(t.GetGenericArguments()[0]));
            }
            else
            {
                if (!CantChange.Contains(t))
                {
                    try
                    {
                        return System.Convert.ChangeType(s, t, CultureInfo.CurrentCulture);
                    }
                    catch (InvalidCastException)
                    {
                        CantChange.Add(t);
                    }
                }

                foreach (MethodInfo m in t.GetMethods())
                {
                    if (m.Name == "Parse" && m.IsStatic)
                    {
                        ParameterInfo[] Params = m.GetParameters();

                        if (Params.Length == 1 && Params.Single().ParameterType == typeof(string))
                        {
                            return m.Invoke(null, new object[] { s });
                        }
                    }
                }
            }

            throw new Exception($"No valid cast path known for type {t}");
        }

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

        private static HashSet<Type> CantChange { get; set; } = new HashSet<Type>();

        static StringExtensions()
        {
            CantChange.Add(typeof(DateTimeOffset));
        }
    }
}
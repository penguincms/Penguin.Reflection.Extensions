using System;
using System.Linq;

namespace Penguin.Reflection.Extensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public static class StringExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        #region Methods

        /// <summary>
        /// Attempts to convert a string to the specified type
        /// </summary>
        /// <typeparam name="T">The type to cast the return value as</typeparam>
        /// <param name="s">The string value</param>
        /// <param name="IgnoreCase">Whether or not case should be ignored (enum)</param>
        /// <returns>A casted representation of the string value</returns>
        public static T Convert<T>(this string s, bool IgnoreCase = false) => (T)s.Convert(typeof(T), IgnoreCase);

        /// <summary>
        /// Converts a string to the requested type. Handles nullables.
        /// </summary>
        /// <param name="s">The string value</param>
        /// <param name="t">The type to cast the value as</param>
        /// <param name="IgnoreCase">Whether or not case should be ignored (enum)</param>
        /// <returns></returns>
        public static object Convert(this string s, Type t, bool IgnoreCase = false)
        {
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
            }

            //I feel like this could be done better by leveraging system types.
            if (string.IsNullOrWhiteSpace(s) && t.IsValueType)
            {
                return t.GetDefaultValue();
            }

            if (s is null && !t.IsValueType)
            {
                return null;
            }

            if (t.IsSubclassOf(typeof(System.Enum)))
            {
                bool NumericString = "0123456789".Contains(s.First());

                if (NumericString)
                {
                    return Enum.Parse(t, s);
                }
                else if (IgnoreCase)
                {
                    return Enum.GetValues(t).Cast<object>().First(e => string.Equals(s, e.ToString(), StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    return Enum.GetValues(t).Cast<object>().First(e => string.Equals(s, e.ToString(), StringComparison.Ordinal));
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
                return System.Convert.ChangeType(s, t);
            }
        }

        #endregion Methods
    }
}
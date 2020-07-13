using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Penguin.Reflection.Extensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public static partial class ObjectExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        /// <summary>
        /// Converts an object to a dictionary and adds a property
        /// </summary>
        /// <param name="o">The source object</param>
        /// <param name="name">The property name</param>
        /// <param name="value">The property value</param>
        /// <returns>A dictionary representation of the object</returns>
        public static IDictionary<string, object> AddProperty(this object o, string name, object value)
        {
            IDictionary<string, object> dictionary = o.ToDictionary();
            dictionary.Add(name, value);
            return dictionary;
        }

        /// <summary>
        /// Converts an object to a dictionary of properties and values
        /// </summary>
        /// <param name="o">The object source</param>
        /// <returns>A dictionary of property names and values</returns>
        public static IDictionary<string, object> ToDictionary(this object o)
        {
            IDictionary<string, object> result = new Dictionary<string, object>();
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(o);
            foreach (PropertyDescriptor property in properties)
            {
                result.Add(property.Name, property.GetValue(o));
            }

            return result;
        }

        public static void Invoke(this object o, string MethodName, BindingFlags flags, params object[] Parameters)
        {
            Invoke<object>(o, MethodName, flags, Parameters);
        }

        public static T Invoke<T>(string MethodName, params object[] Parameters)
        {
            return Invoke<T>(MethodName, BindingFlags.Public, BindingFlags.Instance, Parameters);
        }

        public static T Invoke<T>(this object o, string MethodName, BindingFlags flags, params object[] Parameters)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            Type t = o.GetType();

            List<MethodInfo> methods = t.GetMethods(flags).ToList();

            if (!methods.Any())
            {
                throw new NotImplementedException($"No methods found on object with type {t}");
            }

            methods = methods.Where(m => m.Name == MethodName).ToList();

            if (!methods.Any())
            {
                throw new NotImplementedException($"Method with name {MethodName} not found on object with type {t}");
            }

            MethodInfo toExecute = null;

            foreach (MethodInfo m in methods)
            {
                int i = 0;

                List<ParameterInfo> parameters = m.GetParameters().ToList();

                if (Parameters.Length > parameters.Count)
                {
                    continue;
                }

                foreach (ParameterInfo pi in parameters)
                {
                    if (Parameters.Length <= i)
                    {
                        if (!pi.IsOptional)
                        {
                            break;
                        }
                    }

                    object input = Parameters[i];

                    Type expectedType = pi.ParameterType;

                    if (input != null && !expectedType.IsAssignableFrom(input.GetType()))
                    {
                        break;
                    }

                    i++;
                }
            }

            if (toExecute is null)
            {
                throw new NotImplementedException($"Could not find method matching signature {MethodName}({string.Join(", ", Parameters.Select(p => p is null ? "object?" : p.GetType().GetDeclaration()))})");
            }

            object result = toExecute.Invoke(o, Parameters);

            if (!(result is T))
            {
                throw new InvalidCastException($"Method {MethodName} returned type {(result is null ? "null" : result.GetType().ToString())} but a return type of {typeof(T)} was expected.");
            }

            return (T)result;
        }

        /// <summary>
        /// Attempts to string convert an object into a value that can be consumed by a json serializer
        /// </summary>
        /// <param name="o">The source object</param>
        /// <returns>A Json safe (hopefully) representation</returns>
        public static string ToJSONValue(this object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            string s = o.ToString();
            if (s == null || s.Length == 0)
            {
                return "";
            }

            int i;
            int len = s.Length;
            StringBuilder sb = new StringBuilder(len + 4);
            string t;

            for (i = 0; i < len; i += 1)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;

                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    default:
                        if (c < ' ')
                        {
                            t = "000" + ((byte)c).ToString("X", CultureInfo.CurrentCulture);
                            sb.Append("\\u" + t.Substring(t.Length - 4));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Creates a new instance of T and shallow clones the properties across from the source
        /// </summary>
        /// <typeparam name="T">The type to create an instance of</typeparam>
        /// <param name="source">The property source</param>
        /// <returns>A new instance of T with matching properties mapped</returns>
        public static T ShallowClone<T>(this object source)
        {
            T dest = Activator.CreateInstance<T>();

            source.Populate(dest);

            return dest;
        }

        /// <summary>
        /// Creates a new instance of T and shallow clones the properties across from the source
        /// </summary>
        /// <typeparam name="T">The type to create an instance of</typeparam>
        /// <param name="source">The property source</param>
        /// <returns>A new instance of T with matching properties mapped</returns>
        public static T ShallowClone<T>(this T source)
        {
            T dest = Activator.CreateInstance<T>();

            source.Populate(dest);

            return dest;
        }

        /// <summary>
        /// Shallow copy properties across objects
        /// </summary>
        /// <param name="source">The source of the property values</param>
        /// <param name="dest">The destination of the property values</param>
        public static void Populate(this object source, object dest)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            Type sourceType = source.GetType();
            Type destType = dest.GetType();
            PropertyInfo[] sourceProps = sourceType.GetProperties();

            if (sourceType == destType)
            {
                foreach (PropertyInfo pi in sourceProps)
                {
                    if (pi.GetGetMethod() != null && pi.GetSetMethod() != null)
                    {
                        pi.SetValue(dest, pi.GetValue(source));
                    }
                }
            }
            else
            {
                PropertyInfo[] destProps = destType.GetProperties();

                foreach (PropertyInfo pi in sourceProps)
                {
                    PropertyInfo destProp = destProps.First(d => d.Name == pi.Name);

                    if (pi.GetGetMethod() != null && destProp.GetSetMethod() != null)
                    {
                        destProp.SetValue(dest, pi.GetValue(source));
                    }
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Penguin.Reflection.Extensions
{
    public static partial class ObjectExtensions
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

        /// <summary>
        /// Invokes a method on the object using the given name and parameters
        /// </summary>
        /// <param name="o">The object to invoke the method on</param>
        /// <param name="MethodName">The name of the method</param>
        /// <param name="flags">Binding flags used to find the method</param>
        /// <param name="Parameters">Any method parameters</param>
        public static void Invoke(this object o, string MethodName, BindingFlags flags, params object[] Parameters)
        {
            _ = Invoke<object>(o, MethodName, flags, Parameters);
        }

        public static T Invoke<T>(string MethodName, params object[] Parameters)
        {
            return Invoke<T>(MethodName, BindingFlags.Public, BindingFlags.Instance, Parameters);
        }

        /// <summary>
        /// Invokes a method on the object using the given name and parameters
        /// </summary>
        /// <typeparam name="T">The method return type</typeparam>
        /// <param name="o">The object to invoke the method on</param>
        /// <param name="MethodName">The name of the method</param>
        /// <param name="flags">Binding flags used to find the method</param>
        /// <param name="Parameters">Any method parameters</param>
        /// <returns>The method return</returns>
        public static T Invoke<T>(this object o, string MethodName, BindingFlags flags, params object[] Parameters)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (Parameters is null)
            {
                throw new ArgumentNullException(nameof(Parameters));
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

            return result is not T
                ? throw new InvalidCastException($"Method {MethodName} returned type {(result is null ? "null" : result.GetType().ToString())} but a return type of {typeof(T)} was expected.")
                : (T)result;
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
            StringBuilder sb = new(len + 4);
            string t;

            for (i = 0; i < len; i += 1)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        _ = sb.Append('\\');
                        _ = sb.Append(c);
                        break;

                    case '/':
                        _ = sb.Append('\\');
                        _ = sb.Append(c);
                        break;

                    case '\b':
                        _ = sb.Append("\\b");
                        break;

                    case '\t':
                        _ = sb.Append("\\t");
                        break;

                    case '\n':
                        _ = sb.Append("\\n");
                        break;

                    case '\f':
                        _ = sb.Append("\\f");
                        break;

                    case '\r':
                        _ = sb.Append("\\r");
                        break;

                    default:
                        if (c < ' ')
                        {
                            t = "000" + ((byte)c).ToString("X", CultureInfo.CurrentCulture);
                            _ = sb.Append("\\u" + t[^4..]);
                        }
                        else
                        {
                            _ = sb.Append(c);
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

            PropertyInfo[] sourceProps = sourceType.GetProperties().Where(ValidatePropertyBind).ToArray();

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

        /// <summary>
        /// Returns true if the object represents the default value for its type
        /// </summary>
        /// <param name="o">The object to test</param>
        /// <returns>True if the object represents the default value for its type</returns>
        public static bool IsDefaultValue(this object o)
        {
            return o is null || Equals(o, o.GetType().GetDefaultValue());
        }

        private static bool ValidatePropertyBind(PropertyInfo pi)
        {
            return pi.GetGetMethod() != null && pi.GetSetMethod() != null && pi.GetIndexParameters().Length == 0;
        }
    }
}
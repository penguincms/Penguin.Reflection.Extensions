using Penguin.Reflection.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Penguin.Reflection.Extensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public static class TypeExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        #region Methods

        /// <summary>
        /// Returns a stack of all base types excluding the end type (like object)
        /// </summary>
        /// <param name="start">The type to start recursion at</param>
        /// <param name="end">The exclusive end type for the recursion</param>
        /// <returns>A stack of types including the start type but excluding the end type</returns>
        public static IEnumerable<Type> GetAllBasesExcluding(this Type start, Type end)
        {
            Type toCheck = start;
            while (toCheck.IsSubclassOf(end) && toCheck != end)
            {
                yield return toCheck;
                toCheck = toCheck.BaseType;
            }
        }

        /// <summary>
        /// Returns a stack of all types between the two given types, including the end (if found)
        /// </summary>
        /// <param name="start">The type to start recursion at</param>
        /// <param name="end">The type to end recursion at</param>
        /// <returns>A stack of all types between the two given types</returns>
        public static IEnumerable<Type> GetAllBasesIncluding(this Type start, Type end)
        {
            Type toCheck = start;
            while (toCheck.IsSubclassOf(end))
            {
                yield return toCheck;
                toCheck = toCheck.BaseType;
            }

            if (toCheck != null)
            {
                yield return toCheck;
            }
        }

        /// <summary>
        /// Gets all CONST from the given type, casted to the specified type
        /// </summary>
        /// <typeparam name="T">The type to return</typeparam>
        /// <param name="type">The type to search</param>
        /// <returns>All CONST from the given type, casted to the specified type</returns>
        public static List<T> GetAllPublicConstantValues<T>(this Type type) => type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType is T)
                .Select(x => (T)x.GetRawConstantValue())
                .ToList();

        /// <summary>
        /// Searches the current assembly for all types implementing the generic base class, that can be instantiated
        /// Ex FoundType : baseType&lt;&gt;
        /// </summary>
        /// <param name="baseType">The base type to search for</param>
        /// <returns>All types implementing a generic base class</returns>
        public static List<Type> GetAllTypesImplementingGenericBase(Type baseType)
        {
            IEnumerable<Type> allTypesOfIRepository = Assembly.GetExecutingAssembly()
                                                .GetTypes()
                                                .Where(z => z.BaseType != null &&
                                                            !z.BaseType.IsAbstract &&
                                                            !z.BaseType.IsInterface &&
                                                             z.BaseType.IsGenericType &&
                                                             z.BaseType.GetGenericTypeDefinition() == baseType);

            return allTypesOfIRepository.ToList();
        }

        /// <summary>
        /// Searches the current assembly for all types implementing the generic base class where the base class parameter equals the specified type
        /// Ex FoundType : baseType&lt;thing&gt;
        /// </summary>
        /// <param name="baseType">The base type to search for</param>
        /// <param name="typeParameter">The type the base class must implement</param>
        /// <returns>The aforementioned list</returns>
        public static IEnumerable<Type> GetAllTypesImplementingGenericBase(Type baseType, Type typeParameter) => GetAllTypesImplementingGenericBase(baseType).Where(t => t.BaseType.GenericTypeArguments.Contains(typeParameter));

        /// <summary>
        /// Attempts to resolve a type representation of a collection to retrieve its core unit. Should work on things like Lists as well as Arrays
        /// </summary>
        /// <param name="type">The type to search</param>
        /// <returns>The unit type of the collection</returns>
        public static Type GetCollectionType(this Type type)
        {
            Type itemType = null;
            ;

            if (type.IsArray)
            {
                itemType = type.GetElementType();
            }
            else
            {
                if (type.GetGenericArguments().Any())
                {
                    itemType = type.GetGenericArguments()[0];
                }
                else
                {
                    List<Type> containerTypesToCheck = new List<Type>();

                    if (!type.IsInterface)
                    {
                        containerTypesToCheck.AddRange(type.GetAllBasesExcluding(typeof(object)));
                    }

                    containerTypesToCheck.AddRange(type.GetInterfaces());

                    foreach (Type toCheck in containerTypesToCheck)
                    {
                        if (toCheck.GetGenericArguments().Any())
                        {
                            return toCheck.GetGenericArguments()[0];
                        }
                    }
                }
            }

            return itemType;
        }

        /// <summary>
        /// Gets a list of all fields on the object that are CONST
        /// </summary>
        /// <param name="type">The type to search</param>
        /// <returns>The const fields</returns>
        public static IEnumerable<FieldInfo> GetConstants(this Type type)
        {
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public |
                 BindingFlags.Static | BindingFlags.FlattenHierarchy);

            return fieldInfos.Where(fi => fi.IsLiteral && !fi.IsInitOnly);
        }

        /// <summary>
        /// Attempts to get the type CoreType (simple type representation)
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>Its core type</returns>
        public static CoreType GetCoreType(this Type type)
        {
            if (new List<Type>() { typeof(string), typeof(Guid) }.Contains(type) || (Nullable.GetUnderlyingType(type) != null))
            {
                return CoreType.Value;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type) && (type.IsArray || type.GetGenericArguments().Count() == 1))
            {
                return CoreType.Collection;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return CoreType.Dictionary;
            }
            else if (Type.GetTypeCode(type) == TypeCode.Object)
            {
                return CoreType.Reference;
            }
            else if (type.IsSubclassOf(typeof(Enum)))
            {
                return CoreType.Enum;
            }
            else
            {
                return CoreType.Value;
            }
        }

        /// <summary>
        /// Resolves the default value for a generic type. Dont know why this exists
        /// </summary>
        /// <typeparam name="T">The generic type to check</typeparam>
        /// <returns>The default value for the type</returns>
        public static T GetDefaultValue<T>() => default;

        /// <summary>
        /// Attempts to get the default value for a type by creating an instance
        /// </summary>
        /// <param name="type">The type to get the value for</param>
        /// <returns>The default value for the type</returns>
        public static object GetDefaultValue(this Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        /// <summary>
        /// Literally just removes everything after '`'
        /// </summary>
        /// <param name="t">The type name to get</param>
        /// <returns>The name without the generic part</returns>
        public static string GetNameWithoutGenericArity(this Type t)
        {
            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name.Substring(0, index);
        }

        /// <summary>
        /// An easier to read way to check if a type implements an interface
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="thisInterface">The interface to check for</param>
        /// <returns>Whether or not the type implements the interface</returns>
        public static bool ImplementsInterface(this Type type, Type thisInterface) => thisInterface.IsAssignableFrom(type);

        /// <summary>
        /// An easier to read way to check if a type implements an interface
        /// </summary>
        /// <typeparam name="T">The type to check</typeparam>
        /// <param name="type">The type to check</param>
        /// <returns>Whether or not the type implements the interface</returns>
        public static bool ImplementsInterface<T>(this Type type) => type.ImplementsInterface(typeof(T));

        /// <summary>
        /// Checks if the type supports decimal notation
        /// </summary>
        /// <param name="t">the type to check</param>
        /// <returns>Does the type support decimal notation?</returns>
        public static bool IsDecimalType(this Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Decimal:
                case TypeCode.Double:
                    return true;

                default:
                    return t == typeof(float);
            }
        }

        /// <summary>
        /// Checks if the type matches any of the Numeric types
        /// </summary>
        /// <param name="t">The type to check</param>
        /// <returns>Is the type numeric?</returns>
        public static bool IsNumericType(this Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Is the type static?
        /// </summary>
        /// <param name="type">Is the type static?</param>
        /// <returns></returns>
        public static bool IsStatic(this Type type) => type.IsAbstract && type.IsSealed;

        /// <summary>
        /// Checks to see if the type is a subclass of the given type
        /// </summary>
        /// <typeparam name="T">The possible base type</typeparam>
        /// <param name="type">the type to check</param>
        /// <returns>if the type is a subclass of the given type</returns>
        public static bool IsSubclassOf<T>(this Type type) => type.IsSubclassOf(typeof(T));

        /// <summary>
        /// Checks to see if the type is a subclass of the given type
        /// </summary>
        /// <param name="type">The possible base type</param>
        /// <param name="baseType"></param>
        /// <returns>if the type is a subclass of the given type</returns>
        public static bool IsSubclassOf(this Type type, Type baseType)
        {
            if (type == null || baseType == null || type == baseType)
            {
                return false;
            }

            if (baseType.IsGenericType == false)
            {
                if (type.IsGenericType == false)
                {
                    return type.IsSubclassOf(baseType);
                }
            }
            else
            {
                baseType = baseType.GetGenericTypeDefinition();
            }

            type = type.BaseType;
            Type objectType = typeof(object);

            while (type != objectType && type != null)
            {
                Type curentType = type.IsGenericType ?
                    type.GetGenericTypeDefinition() : type;
                if (curentType == baseType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        #endregion Methods
    }
}
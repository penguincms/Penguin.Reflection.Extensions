﻿using Penguin.Extensions.String;
using Penguin.Reflection.Abstractions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Penguin.Reflection.Extensions
{
    public static partial class TypeExtensions
    {
        internal static ConcurrentDictionary<Type, Type> CollectionTypeCache = new();

        /// <summary>
        /// Returns a stack of all base types excluding the end type (like object)
        /// </summary>
        /// <param name="start">The type to start recursion at</param>
        /// <param name="end">The exclusive end type for the recursion</param>
        /// <returns>A stack of types including the start type but excluding the end type</returns>
        public static IEnumerable<Type> GetAllBasesExcluding(this Type start, Type end)
        {
            if (start is null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            if (end is null)
            {
                throw new ArgumentNullException(nameof(end));
            }

            Type toCheck = start;

            while ((toCheck?.IsSubclassOf(end) ?? false) && toCheck != end)
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
            if (start is null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            if (end is null)
            {
                throw new ArgumentNullException(nameof(end));
            }

            Type toCheck = start;

            while (toCheck?.IsSubclassOf(end) ?? false)
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
        public static List<T> GetAllPublicConstantValues<T>(this Type type)
        {
            return type is null
                ? throw new ArgumentNullException(nameof(type))
                : type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                       .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType is T)
                       .Select(x => (T)x.GetRawConstantValue())
                       .ToList();
        }

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
        public static IEnumerable<Type> GetAllTypesImplementingGenericBase(Type baseType, Type typeParameter)
        {
            return GetAllTypesImplementingGenericBase(baseType).Where(t => t.BaseType.GenericTypeArguments.Contains(typeParameter));
        }

        /// <summary>
        /// Takes in an open type definition and finds all interfaces or base classes that define that open definition and returns them
        /// </summary>
        /// <param name="type">The type to check for implementations</param>
        /// <param name="toFind">The open type definition to search for</param>
        /// <returns>Each defined close type definition for the type being checked, that is an implementation of the search type</returns>
        public static IEnumerable<Type> GetClosedImplementationsFor(this Type type, Type toFind)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (toFind is null)
            {
                throw new ArgumentNullException(nameof(toFind));
            }

            if (!toFind.IsGenericTypeDefinition)
            {
                throw new ArgumentException("This method can only be used on open type definitions");
            }

            if (toFind.IsInterface)
            {
                foreach (Type interfaceType in type.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == toFind)
                    {
                        yield return interfaceType;
                    }
                }
            }
            else
            {
                Type toCheck = type;

                do
                {
                    if (toCheck.IsGenericType && toCheck.GetGenericTypeDefinition() == toFind)
                    {
                        yield return toCheck;
                    }

                    toCheck = toCheck.BaseType;
                } while (toCheck != null);
            }
        }

        /// <summary>
        /// Attempts to resolve a type representation of a collection to retrieve its core unit. Should work on things like Lists as well as Arrays
        /// </summary>
        /// <param name="type">The type to search</param>
        /// <returns>The unit type of the collection</returns>
        public static Type GetCollectionType(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (CollectionTypeCache.TryGetValue(type, out Type itemType))
            {
                return itemType;
            }

            if (type.IsArray)
            {
                itemType = type.GetElementType();
            }
            else
            {
                if ((itemType = type.GetGenericArguments().FirstOrDefault()) == null)
                {
                    if (!type.IsInterface)
                    {
                        foreach (Type t in type.GetAllBasesExcluding(typeof(object)))
                        {
                            if ((itemType = type.GetGenericArguments().FirstOrDefault()) != null)
                            {
                                break;
                            }
                        }
                    }

                    if (itemType is null)
                    {
                        foreach (Type toCheck in type.GetInterfaces())
                        {
                            if ((itemType = toCheck.GetGenericArguments().FirstOrDefault()) != null)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            _ = CollectionTypeCache.TryAdd(type, itemType);

            return itemType;
        }

        /// <summary>
        /// Gets a list of all fields on the object that are CONST
        /// </summary>
        /// <param name="type">The type to search</param>
        /// <returns>The const fields</returns>
        public static IEnumerable<FieldInfo> GetConstants(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

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
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (new List<Type>() { typeof(string), typeof(Guid) }.Contains(type) || (Nullable.GetUnderlyingType(type) != null))
            {
                return CoreType.Value;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type) && (type.IsArray || type.GetGenericArguments().Length == 1))
            {
                return CoreType.Collection;
            }
            else
            {
                return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                    ? CoreType.Dictionary
                    : Type.GetTypeCode(type) == TypeCode.Object
                                    ? CoreType.Reference
                                    : type.IsSubclassOf(typeof(Enum)) ? CoreType.Enum : CoreType.Value;
            }
        }



        /// <summary>
        /// Resolves the default value for a generic type. Dont know why this exists
        /// </summary>
        /// <typeparam name="T">The generic type to check</typeparam>
        /// <returns>The default value for the type</returns>
        public static T GetDefaultValue<T>()
        {
            return default;
        }

        /// <summary>
        /// Attempts to get the default value for a type by creating an instance
        /// </summary>
        /// <param name="type">The type to get the value for</param>
        /// <returns>The default value for the type</returns>
        public static object GetDefaultValue(this Type type)
        {
            return type is null ? throw new ArgumentNullException(nameof(type)) : type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Literally just removes everything after '`'
        /// </summary>
        /// <param name="t">The type name to get</param>
        /// <returns>The name without the generic part</returns>
        public static string GetNameWithoutGenericArity(this Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name[..index];
        }

        /// <summary>
        /// An easier to read way to check if a type implements an interface, includes open generics
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="thisInterface">The interface to check for</param>
        /// <returns>Whether or not the type implements the interface</returns>
        public static bool ImplementsInterface(this Type type, Type thisInterface)
        {
            return type is null
                ? throw new ArgumentNullException(nameof(type))
                : thisInterface is null
                ? throw new ArgumentNullException(nameof(thisInterface))
                : thisInterface.IsGenericTypeDefinition
                ? type.GetInterfaces().Any(x =>
                       x.IsGenericType &&
                       x.GetGenericTypeDefinition() == thisInterface)
                : thisInterface.IsAssignableFrom(type);
        }

        /// <summary>
        /// An easier to read way to check if a type implements an interface
        /// </summary>
        /// <typeparam name="T">The type to check</typeparam>
        /// <param name="type">The type to check</param>
        /// <returns>Whether or not the type implements the interface</returns>
        public static bool ImplementsInterface<T>(this Type type)
        {
            return type.ImplementsInterface(typeof(T));
        }

        /// <summary>
        /// Checks if a given type is anonymous
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the given type is anonymous</returns>
        public static bool IsAnonymousType(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            bool hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any();
            bool nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
            bool isAnonymousType = hasCompilerGeneratedAttribute && nameContainsAnonymousType;

            return isAnonymousType;
        }

        /// <summary>
        /// Checks if the type inherits from ICollection or a Generic ICollection
        /// </summary>
        /// <param name="t">The type to check</param>
        /// <returns>If the type inherits from ICollection or a Generic ICollection</returns>
        public static bool IsCollection(this Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            Type gType;

            return (gType = t.GetGenericArguments().FirstOrDefault()) != null
                ? typeof(ICollection<>).MakeGenericType(gType).IsAssignableFrom(t)
                : typeof(ICollection).IsAssignableFrom(t);
        }

        /// <summary>
        /// Checks if the type inherits from IList or a Generic IList
        /// </summary>
        /// <param name="t">The type to check</param>
        /// <returns>If the type inherits from IList or a Generic IList</returns>
        public static bool IsList(this Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            Type gType;

            return (gType = t.GetGenericArguments().FirstOrDefault()) != null
                ? typeof(IList<>).MakeGenericType(gType).IsAssignableFrom(t)
                : typeof(IList).IsAssignableFrom(t);
        }

        /// <summary>
        /// Checks if the type supports decimal notation
        /// </summary>
        /// <param name="t">the type to check</param>
        /// <returns>Does the type support decimal notation?</returns>
        public static bool IsDecimalType(this Type t)
        {
            return Type.GetTypeCode(t) switch
            {
                TypeCode.Decimal or TypeCode.Double => true,
                _ => t == typeof(float),
            };
        }

        /// <summary>
        /// Checks if the type matches any of the Numeric types
        /// </summary>
        /// <param name="t">The type to check</param>
        /// <returns>Is the type numeric?</returns>
        public static bool IsNumericType(this Type t)
        {
            return Type.GetTypeCode(t) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
                _ => false,
            };
        }

        /// <summary>
        /// Is the type static?
        /// </summary>
        /// <param name="type">Is the type static?</param>
        /// <returns></returns>
        public static bool IsStatic(this Type type)
        {
            return type is null ? throw new ArgumentNullException(nameof(type)) : type.IsAbstract && type.IsSealed;
        }

        /// <summary>
        /// Checks to see if the type is a subclass of the given type
        /// </summary>
        /// <typeparam name="T">The possible base type</typeparam>
        /// <param name="type">the type to check</param>
        /// <returns>if the type is a subclass of the given type</returns>
        public static bool IsSubclassOf<T>(this Type type)
        {
            return type is null ? throw new ArgumentNullException(nameof(type)) : type.IsSubclassOf(typeof(T));
        }

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
    }
}
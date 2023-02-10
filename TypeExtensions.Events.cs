using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Penguin.Reflection.Extensions
{
    /// <summary>
    /// https://www.codeproject.com/Articles/103542/Removing-Event-Handlers-using-Reflection
    /// </summary>
    public static partial class TypeExtensions
    {
        private static readonly Dictionary<Type, List<FieldInfo>> dicEventFieldInfos = new();

        internal static BindingFlags AllBindings => BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        //--------------------------------------------------------------------------------
        internal static void BuildEventFields(this Type t, List<FieldInfo> lst)
        {
            // Type.GetEvent(s) gets all Events for the type AND it's ancestors
            // Type.GetField(s) gets only Fields for the exact type.
            //  (BindingFlags.FlattenHierarchy only works on PROTECTED & PUBLIC
            //   doesn't work because Fieds are PRIVATE)

            // NEW version of this routine uses .GetEvents and then uses .DeclaringType
            // to get the correct ancestor type so that we can get the FieldInfo.
            foreach (EventInfo ei in t.GetEvents(AllBindings))
            {
                Type dt = ei.DeclaringType;
                FieldInfo fi = dt.GetField(ei.Name, AllBindings);
                if (fi != null)
                {
                    lst.Add(fi);
                }
            }

            // OLD version of the code - called itself recursively to get all fields
            // for 't' and ancestors and then tested each one to see if it's an EVENT
            // Much less efficient than the new code
            /*
                  foreach (FieldInfo fi in t.GetFields(AllBindings))
                  {
                    EventInfo ei = t.GetEvent(fi.Name, AllBindings);
                    if (ei != null)
                    {
                      lst.Add(fi);
                      Console.WriteLine(ei.Name);
                    }
                  }
                  if (t.BaseType != null)
                    BuildEventFields(t.BaseType, lst);*/
        }

        //--------------------------------------------------------------------------------
        internal static EventHandlerList GetStaticEventHandlerList(this Type t, object obj)
        {
            MethodInfo mi = t.GetMethod("get_Events", AllBindings);
            return (EventHandlerList)mi.Invoke(obj, Array.Empty<object>());
        }

        //--------------------------------------------------------------------------------
        internal static List<FieldInfo> GetTypeEventFields(this Type t)
        {
            if (dicEventFieldInfos.TryGetValue(t, out List<FieldInfo> value))
            {
                return value;
            }

            List<FieldInfo> lst = new();
            BuildEventFields(t, lst);
            dicEventFieldInfos.Add(t, lst);
            return lst;
        }
    }
}
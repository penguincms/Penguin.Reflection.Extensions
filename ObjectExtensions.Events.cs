using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Penguin.Reflection.Extensions
{
    public static partial class ObjectExtensions
    {
        //--------------------------------------------------------------------------------
        public static void RemoveAllEventHandlers(this object obj) { RemoveEventHandler(obj, ""); }

        //--------------------------------------------------------------------------------
        public static void RemoveEventHandler(this object obj, string EventName)
        {
            if (obj == null)
            {
                return;
            }

            Type t = obj.GetType();
            List<FieldInfo> event_fields = t.GetTypeEventFields();
            EventHandlerList static_event_handlers = null;

            foreach (FieldInfo fi in event_fields)
            {
                if (!string.IsNullOrWhiteSpace(EventName) && string.Compare(EventName, fi.Name, true) != 0)
                {
                    continue;
                }

                // After hours and hours of research and trial and error, it turns out that
                // STATIC Events have to be treated differently from INSTANCE Events...
                if (fi.IsStatic)
                {
                    // STATIC EVENT
                    if (static_event_handlers == null)
                    {
                        static_event_handlers = t.GetStaticEventHandlerList(obj);
                    }

                    object idx = fi.GetValue(obj);
                    Delegate eh = static_event_handlers[idx];
                    if (eh == null)
                    {
                        continue;
                    }

                    Delegate[] dels = eh.GetInvocationList();
                    if (dels == null)
                    {
                        continue;
                    }

                    EventInfo ei = t.GetEvent(fi.Name, TypeExtensions.AllBindings);
                    foreach (Delegate del in dels)
                    {
                        ei.RemoveEventHandler(obj, del);
                    }
                }
                else
                {
                    // INSTANCE EVENT
                    EventInfo ei = t.GetEvent(fi.Name, TypeExtensions.AllBindings);
                    if (ei != null)
                    {
                        object val = fi.GetValue(obj);
                        Delegate mdel = (val as Delegate);
                        if (mdel != null)
                        {
                            foreach (Delegate del in mdel.GetInvocationList())
                            {
                                ei.RemoveEventHandler(obj, del);
                            }
                        }
                    }
                }
            }
        }

        //--------------------------------------------------------------------------------
    }
}
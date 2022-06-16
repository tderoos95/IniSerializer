using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealUniverse.UT2004.IniSerializer
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Creates a clone of the given list.
        /// Caution: these are NOT deep copies, so this only works for strings and structs.
        /// <returns></returns>
        public static IList Clone(this IList list)
        {
            Type listType = list.GetType();
            IList readOnlyList =  Activator.CreateInstance(listType) as IList;

            foreach (object item in list)
            {
                readOnlyList.Add(item);
            }

            return readOnlyList;
        }
    }
}

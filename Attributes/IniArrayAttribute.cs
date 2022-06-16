using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealUniverse.UT2004.IniSerializer.Attributes
{
    public enum ArrayLoadOptions
    {
        None,
        StripEmptyEntries
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class IniArrayAttribute : Attribute
    {
        public ArrayLoadOptions LoadOptions { get; set; } = ArrayLoadOptions.None;

        public int ArrayLength { get; set; } = 0;

        public void ApplyAttributeForLoad(IList list, Type listItemType)
        {
            switch (LoadOptions)
            {
                case ArrayLoadOptions.StripEmptyEntries:
                    StripEmptyEntries(list, listItemType);
                    break;
            }
        }

        private void StripEmptyEntries(IList list, Type listItemType)
        {
            IList readOnlyList = list.Clone();

            foreach (object item in readOnlyList)
            {
                if (listItemType == typeof(string))
                {
                    if(string.IsNullOrEmpty(item as string))
                        list.Remove(item);
                }
                else if (listItemType.IsValueType)
                {
                    if(item == null)
                        list.Remove(item);  // todo test this scenario
                }
                else
                    throw new NotImplementedException();
            }
        }

        public void ApplyAttributeForSave(IList list, Type listItemType)
        {
            if (ArrayLength != 0)
                EnsureLengthIsMatched(list, listItemType);
        }

        private void EnsureLengthIsMatched(IList list, Type listItemType)
        {
            if (list.Count < ArrayLength)
            {
                int missingAmountOfEntries = ArrayLength - list.Count;
                object emptyListItem = null;

                if (listItemType == typeof(String))
                    emptyListItem = string.Empty;
                else if (listItemType.IsValueType)
                    emptyListItem = Activator.CreateInstance(listItemType); // todo test this scenario
                else
                    throw new NotImplementedException();

                for (int i = 0; i < missingAmountOfEntries; i++)
                {
                    list.Add(emptyListItem);
                }
            }
        }
    }
}

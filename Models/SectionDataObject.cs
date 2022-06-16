using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnrealUniverse.UT2004.IniSerializer.Attributes;

namespace UnrealUniverse.UT2004.IniSerializer.Models
{
    public class SectionDataObject
    {
        [IniIgnore]
        public string SectionName { get; set; }

        [IniIgnore]
        public ExpandoObject Data { get; set; }

        public virtual void SerializeToInstance() { }

        public virtual void SerializeToExpandoObject() { }
    }

    public class SectionDataObject<TDataObject> : SectionDataObject
        where TDataObject : class
    {
        public override void SerializeToInstance()
        {
            Serializer.SerializeDynamicToObject<TDataObject>(Data, this as TDataObject);
        }

        public override void SerializeToExpandoObject()
        {
            object[] attributes = typeof(TDataObject).GetCustomAttributes(typeof(IniSerializableDataAttribute), false);

            SerializationOptions serializationOptions = attributes.Length > 0 ? 
                ((IniSerializableDataAttribute)attributes[0]).SerializationOptions : 
                SerializationOptions.NonDestructive;

            if(serializationOptions == SerializationOptions.NonDestructive)
            {
                ExpandoObject alteredData = Serializer.SerializeObjectToDynamic(this);
                var dataDictionary = (IDictionary<string, object>)Data;

                foreach (KeyValuePair<string, object> alteredPair in alteredData)
                {
                    if(dataDictionary.ContainsKey(alteredPair.Key))
                        dataDictionary[alteredPair.Key] = alteredPair.Value;
                    else 
                        dataDictionary.Add(alteredPair.Key, alteredPair.Value);
                }
            }
            else if(serializationOptions == SerializationOptions.DefinedPropertiesOnly)
            {
                Data = Serializer.SerializeObjectToDynamic(this);
            }
        }
    }
}

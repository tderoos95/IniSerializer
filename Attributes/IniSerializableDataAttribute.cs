using System;

namespace UnrealUniverse.UT2004.IniSerializer.Attributes
{
    public enum SerializationOptions
    {
        /// <summary>
        /// Serializes back all keys and values, even the ones that weren't defined by properties
        /// </summary>
        NonDestructive,
        /// <summary>
        /// Only serializes back the defined properties
        /// </summary>
        DefinedPropertiesOnly
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal class IniSerializableDataAttribute : Attribute
    {
        public SerializationOptions SerializationOptions { get; set; } = SerializationOptions.NonDestructive;
    }
}

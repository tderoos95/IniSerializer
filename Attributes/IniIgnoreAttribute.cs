using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealUniverse.UT2004.IniSerializer.Attributes
{
    /// <summary>
    /// Ignores this property when serializing
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IniIgnoreAttribute : Attribute
    { }
}

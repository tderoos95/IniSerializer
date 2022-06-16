using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UnrealUniverse.UT2004.IniSerializer.Attributes;
using UnrealUniverse.UT2004.IniSerializer.Models;

namespace UnrealUniverse.UT2004.IniSerializer
{
    public static class Serializer
    {
        // todo support multiple inis <Type (sourceType such as BunkerBuildingIniFile), List<Type> (such as InvasionXDataSection)>
        public static Dictionary<string, Type> SectionDefinitions = new Dictionary<string, Type>();
        public static Dictionary<string, Type> DataObjectDefinitions = new Dictionary<string, Type>();

        public static void RegisterSectionDefinition<TType>(string key)
            where TType : SectionDataObject
        {
            if(!SectionDefinitions.ContainsKey(key))
                SectionDefinitions.Add(key, typeof(TType));
        }

        public static void RegisterDataObjectDefinition<TType>()
           where TType : PerObjectConfigDataObject
        {
            string key = typeof(TType).FullName;

            int dotIndex = key.LastIndexOf('.');
            if (dotIndex > 0)
                key = key.Substring(dotIndex + 1);

            RegisterDataObjectDefinition<TType>(key);
        }

        public static void RegisterDataObjectDefinition<TType>(string key)
            where TType : PerObjectConfigDataObject
        {
            if (!DataObjectDefinitions.ContainsKey(key))
                DataObjectDefinitions.Add(key, typeof(TType));
        }

        public static TDestObject SerializeDynamicToObject<TDestObject>(dynamic dataSection)
                where TDestObject : new()
        {
            TDestObject serializedObject = new TDestObject();

            return SerializeDynamicToObject(dataSection, serializedObject);
        }

        private static void ApplyAttributesForLoad(PropertyInfo propertyInfo, IList list, Type listItemType)
        {
            IniArrayAttribute arrayAttribute = propertyInfo.GetCustomAttribute(typeof(IniArrayAttribute))
                                                                as IniArrayAttribute;

            arrayAttribute?.ApplyAttributeForLoad(list, listItemType);
        }

        public static TDestObject SerializeDynamicToObject<TDestObject>(dynamic dataSection, TDestObject serializedObject)
        {
            IDictionary<string, object> data = dataSection;

            foreach (var pair in data)
            {
                PropertyInfo propertyInfo = typeof(TDestObject).GetProperty(pair.Key);

                if (propertyInfo != null)
                {
                    if (pair.Value.GetType() == typeof(List<object>))
                    {
                        Type listType = propertyInfo.PropertyType;
                        Type listItemType = null;

                        try
                        {
                            listItemType = propertyInfo.PropertyType.GetGenericArguments()[0];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            throw new InvalidCastException(string.Format("Expected type for {0}.{1} is List<{2}>, but type is defined as {2} instead", 
                                propertyInfo.DeclaringType.Name, pair.Key, listType.Name));
                        }
                        catch(Exception exception)
                        {
                            throw exception;
                        }

                        IList createdList = Activator.CreateInstance(listType) as IList;

                        foreach(dynamic entry in (List<object>)pair.Value)
                        {
                            dynamic listItem = null;

                            // check for structs, structs have no constructor
                            if(listItemType == typeof(String))
                            {
                                if (string.IsNullOrEmpty((String)entry))
                                    listItem = string.Empty;
                                else listItem = (String)entry;

                                createdList.Add(listItem);
                            }
                            else if (listItemType.IsValueType)
                            {
                                if (listItemType == typeof(int))
                                    listItem = (int)entry;
                                else if (listItemType == typeof(bool))
                                    listItem = (bool)entry;
                                else if (listItemType == typeof(float))
                                    listItem = (float)entry;
                                else
                                    throw new InvalidCastException();

                                createdList.Add(listItem);
                            }
                            // class definitions, anything with a constructor
                            else
                            {
                                listItem = (dynamic)Activator.CreateInstance(listItemType);

                                dynamic serializedListItem = SerializeDynamicToObject(entry, listItem);
                                createdList.Add(serializedListItem);
                            }
                        }

                        ApplyAttributesForLoad(propertyInfo, createdList, listItemType);
                        propertyInfo.SetValue(serializedObject, createdList);
                    }
                    else if(pair.Value.GetType() == typeof(ExpandoObject))
                    {
                        bool isListType = propertyInfo.PropertyType.IsArray || propertyInfo.PropertyType.IsGenericType;

                        if (isListType) // list type, but not recognized as such because there's only 1 array value
                        {
                            Type listType = propertyInfo.PropertyType;
                            Type listItemType = propertyInfo.PropertyType.GetGenericArguments()[0];

                            IList createdList = Activator.CreateInstance(listType) as IList;
                            dynamic listItem = (dynamic)Activator.CreateInstance(listItemType);
                            Serializer.SerializeDynamicToObject(pair.Value, listItem);

                            createdList.Add(listItem);
                            ApplyAttributesForLoad(propertyInfo, createdList, listItemType);
                            propertyInfo.SetValue(serializedObject, createdList);
                            
                            continue;
                        }

                        throw new InvalidCastException();
                    }
                    else
                    {
                        bool isListType = propertyInfo.PropertyType.IsArray || propertyInfo.PropertyType.IsGenericType;

                        if (isListType) // list type, but not recognized as such because there's only 1 array value
                        {
                            Type listType = propertyInfo.PropertyType;
                            Type listItemType = propertyInfo.PropertyType.GetGenericArguments()[0];

                            IList createdList = Activator.CreateInstance(listType) as IList;
                            dynamic listItem;

                            if (listItemType == typeof(String))
                            {
                                if (string.IsNullOrEmpty((String)pair.Value))
                                    listItem = string.Empty;
                                else listItem = (String)pair.Value;

                                createdList.Add(listItem);
                            }
                            else if (listItemType.IsValueType)
                            {
                                if (listItemType == typeof(int))
                                    listItem = (int)pair.Value;
                                else if (listItemType == typeof(bool))
                                    listItem = (bool)pair.Value;
                                else if (listItemType == typeof(float))
                                    listItem = (float)pair.Value;
                                else
                                    throw new InvalidCastException();

                                createdList.Add(listItem);
                            }
                            else
                            {
                                listItem = (dynamic)Activator.CreateInstance(listItemType);
                                Serializer.SerializeDynamicToObject(pair.Value, listItem);
                            }

                            createdList.Add(listItem);
                            ApplyAttributesForLoad(propertyInfo, createdList, listItemType);
                            propertyInfo.SetValue(serializedObject, createdList);

                            continue;
                        }

                        propertyInfo.SetValue(serializedObject, pair.Value);
                    }
                }
            }

            return serializedObject;
        }

        public static List<TListType> SerializeDynamicToList<TListType>(dynamic source)
            where TListType : new()
        {
            List<dynamic> sourceList = (List<dynamic>)source;
            List<TListType> list = new List<TListType>();

            foreach (var entry in sourceList)
            {
                TListType listEntry = SerializeDynamicToObject<TListType>(entry);

                list.Add(listEntry);
            }

            return list;
        }

        private static void ApplyAttributesForSave(PropertyInfo propertyInfo, IList list, Type listItemType)
        {
            IniArrayAttribute arrayAttribute = propertyInfo.GetCustomAttribute(typeof(IniArrayAttribute))
                                                                as IniArrayAttribute;

            arrayAttribute?.ApplyAttributeForSave(list, listItemType);
        }

        public static dynamic SerializeObjectToDynamic(object dataSource)
        {
            dynamic dynamicData = new ExpandoObject();
            var dataSectionDictionary = (IDictionary<string, object>)dynamicData;

            var propertyInfos = dataSource.GetType().GetProperties( // todo do I need all these flags?
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (PropertyInfo propertyInfo in propertyInfos.Where(x => x.GetMethod != null))
            {
                bool hasIgnoreAttribute = propertyInfo.CustomAttributes.Any(x => x.AttributeType == typeof(IniIgnoreAttribute));

                if (!hasIgnoreAttribute)
                {
                    string name = propertyInfo.Name;
                    object value = propertyInfo.GetValue(dataSource);
                    Type valueType = value.GetType();

                    // check for lists/IEnumerables
                    if (typeof(IEnumerable).IsAssignableFrom(valueType) && valueType != typeof(String))
                    {
                        IList list = (IList)value;
                        Type listItemType = valueType.GetGenericArguments()[0];
                        List<object> objectList = new List<object>();

                        ApplyAttributesForSave(propertyInfo, list, listItemType);

                        foreach (object entry in list)
                        {
                            // strings & structs
                            if (listItemType == typeof(String) || listItemType.IsValueType)
                            {
                                objectList.Add(entry);
                            }
                            else
                            {
                                dynamic serializedEntry = SerializeObjectToDynamic(entry);
                                objectList.Add(serializedEntry);
                            }
                        }

                        dataSectionDictionary.Add(name, objectList);
                    }
                    else
                    {
#if DEBUG
                        var matchingMismatchEntry = dataSectionDictionary
                                                        .Select(x => x.Key)
                                                        .FirstOrDefault(x => x.ToUpperInvariant() == name.ToUpperInvariant());

                        if (matchingMismatchEntry != null)
                        {
                            throw new Exception(string.Format("Mismatching upper/lower case in variable name; the property is" +
                                                               " named '{0}' but should be named '{1}' instead.", name,
                                                               matchingMismatchEntry));
                        }
#endif

                        dataSectionDictionary.Add(name, value);
                    }
                }
            }

            return dynamicData;
        }
    }
}

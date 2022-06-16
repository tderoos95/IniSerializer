using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnrealUniverse.UT2004.IniSerializer.Models;

namespace UnrealUniverse.UT2004.IniSerializer
{
    public class IniFile
    {
        private readonly Encoding DefaultEncoding = Encoding.GetEncoding(1252); // ANSI

        public List<SectionDataObject> Sections { get; set; }

        public List<PerObjectConfigDataObject> DataObjects { get; set; }

        public FileInfo FileInfo { get; set; }

        private const string AssignValueCharacter = "=";

        public Dictionary<string, List<string>> sectionPredefinedArrayLengthKeys; // todo attribute

        public Dictionary<string, List<string>> dataObjectPredefinedArrayLengthKeys; // todo auto?

        public IniFile()
        {
            InitializeVariables();
        }

        public IniFile(string path)
        {
            InitializeVariables();
            Load(path);
        }

        public TSectionType GetSection<TSectionType>()
            where TSectionType : SectionDataObject
        {
            return Sections.FirstOrDefault(x =>
                x.GetType() == typeof(TSectionType))
                    as TSectionType;
        }

        public List<TSectionType> GetSections<TSectionType>()
            where TSectionType : SectionDataObject
        {
            return Sections.Where(x =>
                x.GetType() == typeof(TSectionType))
                    .Cast<TSectionType>()
                    .ToList();
        }

        public List<TDataObject> GetDataObjects<TDataObject>()
            where TDataObject : PerObjectConfigDataObject
        {
            return DataObjects.Where(x =>
                x.GetType() == typeof(TDataObject))
                    .Cast<TDataObject>()
                    .ToList();
        }

        protected void InitializeVariables()
        {
            Sections = new List<SectionDataObject>();
            DataObjects = new List<PerObjectConfigDataObject>();
            sectionPredefinedArrayLengthKeys = new Dictionary<string, List<string>>();
            dataObjectPredefinedArrayLengthKeys = new Dictionary<string, List<string>>();
        }

        private static string currentSectionName = string.Empty;

        public List<TType> GetList<TType>(object dynamic)
        {
            if (dynamic == null)
            {
                return null;
            }

            if (dynamic.GetType() == typeof(List<TType>))
            {
                return dynamic as List<TType>;
            }
            else
            {
                List<TType> list = new List<TType>();

                if (dynamic.GetType() == typeof(TType))
                {
                    list.Add((TType)dynamic);
                }

                return list;
            }
        }
        public virtual void Load(string path)
        {
            FileInfo = new FileInfo(path);

            if (!FileInfo.Exists)
                return;

            string[] lines = File.ReadAllLines(path, DefaultEncoding);
            currentSectionName = string.Empty;
            KeyValuePair<string, string>? currentPerObjectConfig = null;

            sectionPredefinedArrayLengthKeys = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> sectionArrayKeys = GetDuplicateKeys(lines);

            SectionDataObject section = null;
            PerObjectConfigDataObject dataObject = null;

            foreach (string line in lines)
            {
                if (line.StartsWith(";"))
                {
                    continue;
                }
                else if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // reset where we're at
                    currentSectionName = string.Empty;
                    section = null;
                    currentPerObjectConfig = null;
                    dataObject = null;

                    bool isPerObjectConfigDefinition = line.IndexOf(" ") != -1; //[VarietyInvasion WaveSetupDataObject]

                    if (isPerObjectConfigDefinition)
                    {
                        int spaceIndex = line.LastIndexOf(" ");

                        if (line.Length > spaceIndex + 1)
                        {
                            int amountOfRemainingCharacters = line.Length - (spaceIndex + 1);
                            string dataObjectName = line.Substring(spaceIndex + 1, amountOfRemainingCharacters - 1);
                            string name = line.Substring(1, spaceIndex - 1);

                            currentPerObjectConfig = new KeyValuePair<string, string>(dataObjectName, name);
                            dataObject = GetOrCreatePerObjectConfigDataObject(currentPerObjectConfig.Value.Value, currentPerObjectConfig.Value.Key);
                            // todo remove this variable
                            currentSectionName = line.Substring(1, line.Length - 2); // store absolute section name for predefined/inline array checks
                        }
                    }
                    else
                    {
                        currentSectionName = line.Substring(1, line.Length - 2);
                        section = GetOrCreateSection(currentSectionName);
                    }
                }
                else if (section != null || dataObject != null)
                {
                    int assignValueCharacterIndex = line.IndexOf(AssignValueCharacter);

                    if (assignValueCharacterIndex != -1)
                    {
                        IDictionary<string, object> data = null;

                        if (section != null)
                            data = section.Data;
                        else if (dataObject != null)
                            data = dataObject.Data;

                        if (data == null)
                            continue;

                        // todo dissect to methods
                        string key = line.Substring(0, assignValueCharacterIndex);

                        bool isInlineArray = HasArrayIndexer(key);
                        if (isInlineArray)
                        {
                            key = key.Substring(0, key.IndexOf("[")); // remove square hooks
                            AddPredefinedArrayLengthKey(currentSectionName, key);
                        }

                        bool isArray = isInlineArray ||
                                       (sectionArrayKeys.ContainsKey(currentSectionName) &&
                                       sectionArrayKeys[currentSectionName].Any(x => x == key));

                        if (!data.TryGetValue(key, out object currentTrueValue))
                        {
                            if (line.Length > assignValueCharacterIndex)
                            {
                                string value = line.Substring(assignValueCharacterIndex + 1);
                                currentTrueValue = GetTrueValue(value);

                                if (isArray)
                                {
                                    List<object> valueList = new List<object>()
                                    {
                                        currentTrueValue
                                    };

                                    data.Add(key, valueList);
                                }
                                else
                                {
                                    data.Add(key, currentTrueValue);
                                }
                            }
                        }
                        else if (isArray)
                        {
                            if (line.Length > assignValueCharacterIndex)
                            {
                                List<object> valueList = data[key] as List<object>;

                                if (valueList != null)
                                {
                                    string value = line.Substring(assignValueCharacterIndex + 1);
                                    object trueValue = GetTrueValue(value);

                                    valueList.Add(trueValue);
                                }
                                else
                                {
                                    var actualType = data[key].GetType();
                                    string message = string.Format("The array key {0}.{1} has already been defined as a {2}.\n" +
                                                                   "Can't cast {0}.{1} from {2} to List<object>. " +
                                                                   "Remove keys named '{1}' with inconsistent value types",
                                                                    currentSectionName, key, actualType);

                                    throw new InvalidCastException(message);
                                }
                            }
                        }
                    }
                }
            }

            ProcessDataForLoad();
        }

        protected virtual void ProcessDataForLoad()
        {
            Sections.ForEach(x => x.SerializeToInstance());
            DataObjects.ForEach(x => x.SerializeToInstance());
        }

        private Dictionary<string, List<string>> GetDuplicateKeys(string[] lines)
        {
            Dictionary<string, List<string>> processedKeys = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> sectionDuplicateKeys = new Dictionary<string, List<string>>();

            currentSectionName = string.Empty;

            foreach (string line in lines)
            {
                if (line.StartsWith(";") || line.Trim().Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSectionName = line.Substring(1, line.Length - 2);
                }
                else if (currentSectionName.Length > 0)
                {
                    int assignValueCharacterIndex = line.IndexOf(AssignValueCharacter);

                    if (assignValueCharacterIndex != -1)
                    {
                        string key = line.Substring(0, assignValueCharacterIndex);

                        if (!processedKeys.TryGetValue(currentSectionName, out List<string> sectionProcessedKeys))
                        {
                            sectionProcessedKeys = new List<string>();
                            processedKeys.Add(currentSectionName, sectionProcessedKeys);
                        }

                        if (!sectionProcessedKeys.Contains(key))
                        {
                            sectionProcessedKeys.Add(key);
                        }
                        else
                        {
                            if (!sectionDuplicateKeys.TryGetValue(currentSectionName, out List<string> duplicateKeys))
                            {
                                duplicateKeys = new List<string>();
                                sectionDuplicateKeys.Add(currentSectionName, duplicateKeys);
                            }

                            if (!duplicateKeys.Contains(key))
                            {
                                duplicateKeys.Add(key);
                            }
                        }
                    }
                }
            }

            return sectionDuplicateKeys;
        }

        public PerObjectConfigDataObject GetOrCreatePerObjectConfigDataObject(string name, string dataObjectTypeName)
        {
            PerObjectConfigDataObject dataObject = DataObjects.FirstOrDefault(x =>
            x.DataObjectName == name &&
            x.DataObjectType == dataObjectTypeName);

            if (dataObject == null)
            {
                KeyValuePair<string, Type> dataDefinition = Serializer.DataObjectDefinitions.FirstOrDefault(x => x.Key == dataObjectTypeName);

                if (dataDefinition.Key != null && dataDefinition.Value != null)
                {
                    Type dataObjectType = dataDefinition.Value;
                    dataObject = (PerObjectConfigDataObject)Activator.CreateInstance(dataObjectType);
                }
                else
                {
                    dataObject = new PerObjectConfigDataObject();
                }

                dataObject.DataObjectName = name;
                dataObject.DataObjectType = dataObjectTypeName;
                dataObject.Data = new ExpandoObject();

                DataObjects.Add(dataObject);
            }

            return dataObject;
        }

        public SectionDataObject GetOrCreateSection(string sectionName)
        {
            SectionDataObject section = Sections.FirstOrDefault(x => x.SectionName == sectionName);

            if(section == null)
            {
                KeyValuePair<string, Type> dataDefinition = Serializer.SectionDefinitions.FirstOrDefault(x => x.Key == sectionName);

                if (dataDefinition.Key != null && dataDefinition.Value != null)
                {
                    Type dataObjectType = dataDefinition.Value;
                    section = (SectionDataObject)Activator.CreateInstance(dataObjectType);
                }
                else
                {
                    section = new SectionDataObject();
                }

                section.SectionName = sectionName;
                section.Data = new ExpandoObject();

                Sections.Add(section);
            }

            return section;
        }

        private bool HasArrayIndexer(string key)
        {
            return !key.StartsWith("[") &&
                   key.EndsWith("]") &&
                   key.IndexOf("[") != -1;
        }

        private void AddPredefinedArrayLengthKey(string sectionName, string key)
        {
            if (!sectionPredefinedArrayLengthKeys.TryGetValue(sectionName, out List<string> inlineArrayKeys))
            {
                sectionPredefinedArrayLengthKeys.Add(sectionName, new List<string>()
                {
                    key
                });
            }
            else
            {
                if (!inlineArrayKeys.Contains(key))
                {
                    inlineArrayKeys.Add(key);
                }
            }
        }

        // todo remove unnecessary dynamic types
        private object InterpretStringAsObject(string value)
        {
            object dataObject = new ExpandoObject();
            IDictionary<string, object> data = (IDictionary<string, object>)dataObject;

            value = value.Substring(1, value.Length - 2); // remove round hooks
            int nextCommaIndex = value.IndexOf(",");

            if (nextCommaIndex == -1)
            {
                // todo 1 way to do this
                int assignValueCharacterIndex = value.IndexOf(AssignValueCharacter);

                if (assignValueCharacterIndex != -1)
                {
                    string currentKey = value.Substring(0, assignValueCharacterIndex);

                    int currentValueEndIndex = (value.Length - assignValueCharacterIndex) - 1;
                    string currentValue = value.Substring(assignValueCharacterIndex + 1, currentValueEndIndex);

                    object currentTrueValue = GetTrueValue(currentValue);

                    data.Add(currentKey, currentTrueValue);
                }
            }
            else
            {
                while (nextCommaIndex != -1)
                {
                    int assignValueCharacterIndex = value.IndexOf(AssignValueCharacter);

                    if (assignValueCharacterIndex != -1)
                    {
                        string currentKey = value.Substring(0, assignValueCharacterIndex);

                        bool hasPredefinedArrayLength = HasArrayIndexer(currentKey);
                        if (hasPredefinedArrayLength)
                        {
                            currentKey = currentKey.Substring(0, currentKey.IndexOf("[")); // remove square hooks
                            AddPredefinedArrayLengthKey(currentSectionName, currentKey);
                        }

                        object currentTrueValue = null;

                        if (value.Length > assignValueCharacterIndex + 1)
                        {
                            string currentEntry;

                            // struct nested in struct
                            if (value[assignValueCharacterIndex + 1] == '(')
                            {
                                //======
                                // todo come up with reliable method, only supports 1 struct in a struct now
                                int objectEndIndex = value.IndexOf(")");
                                currentEntry = value.Substring(0, objectEndIndex + 1);

                                string residue = value.Substring(objectEndIndex + 1);
                                nextCommaIndex = residue.IndexOf(",") + currentEntry.Length;
                                //======
                            }
                            else
                            {
                                currentEntry = value.Substring(0, nextCommaIndex);
                            }

                            string stringValue = currentEntry.Substring(assignValueCharacterIndex + 1);
                            currentTrueValue = GetTrueValue(stringValue);
                        }

                        if (hasPredefinedArrayLength)
                        {
                            if (!data.ContainsKey(currentKey))
                            {
                                List<object> valueList = new List<object>()
                                {
                                    currentTrueValue
                                };

                                data.Add(currentKey, valueList);
                            }
                            else
                            {
                                List<object> valueList = data[currentKey] as List<object>;

                                valueList.Add(currentTrueValue);
                            }
                        }
                        else
                        {
                            data.Add(currentKey, currentTrueValue);
                        }
                    }

                    if (value.Length > nextCommaIndex)
                    {
                        value = value.Substring(nextCommaIndex + 1);
                        nextCommaIndex = value.IndexOf(",");

                        // also try to parse last value if there is one
                        if (nextCommaIndex == -1 && value.Length > 0)
                        {
                            nextCommaIndex = value.Length;
                        }
                    }
                    else break;
                }
            }

            return dataObject;
        }

        private object GetTrueValue(string value)
        {
            object trueValue;

            if (bool.TryParse(value, out bool bValue))
            {
                trueValue = bValue;
            }
            else if (value.IndexOf(".") != -1 && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fValue))
            {
                trueValue = fValue;
            }
            else if (int.TryParse(value, out int intValue))
            {
                trueValue = intValue;
            }
            else
            {
                // structs
                if (value.StartsWith("(") && value.EndsWith(")"))
                {
                    trueValue = InterpretStringAsObject(value); // remove round hooks
                }
                // strings
                else if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    trueValue = value.Substring(1, value.Length - 2); // remove quotation marks
                }
                else
                {
                    trueValue = value;
                }
            }

            return trueValue;
        }

        public void Save(string path, bool overwrite = false)
        {
            if (File.Exists(path))
            {
                if (overwrite)
                    File.Delete(path);
                else throw new Exception("File already exists");
            }

            string data = ExportToString();
            File.WriteAllText(path, data, DefaultEncoding);
        }

        public string ExportToString()
        {
            ProcessDataForSave();

            StringBuilder sb = new StringBuilder();

            foreach (SectionDataObject section in Sections)
            {
                sb.AppendLine(string.Format("[{0}]", section.SectionName));

                // todo DynamicToString
                foreach (KeyValuePair<string, object> valuePair in section.Data)
                {
                    string name = valuePair.Key;
                    object value = valuePair.Value;

                    string stringValue = ObjectToString(section.SectionName, name, value);

                    if (stringValue.Length > 0)
                        sb.AppendLine(stringValue);
                }

                sb.AppendLine();
            }

            foreach (PerObjectConfigDataObject dataObject in DataObjects)
            {
                string sectionName = dataObject.GetSectionName();
                sb.AppendLine(string.Format("[{0}]", sectionName));

                // todo DynamicToString
                foreach (KeyValuePair<string, object> valuePair in dataObject.Data)
                {
                    string name = valuePair.Key;
                    object value = valuePair.Value;

                    string stringValue = ObjectToString(sectionName, name, value);

                    if (stringValue.Length > 0)
                        sb.AppendLine(stringValue);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        protected virtual void ProcessDataForSave()
        {
            Sections.ForEach(x => x.SerializeToExpandoObject());
            DataObjects.ForEach(x => x.SerializeToExpandoObject());
        }

        public string ObjectToString(string sectionName, string valueName, object value, bool isNestedValue = false)
        {
            if (value == null)
            {
                return string.Format("{0}=", valueName);
            }
            else if (value.GetType() == typeof(ExpandoObject))
            {
                ExpandoObject structValue = value as ExpandoObject;
                StringBuilder structStringBuilder = new StringBuilder();

                IDictionary<string, object> objectValues = (IDictionary<string, object>)structValue;

                structStringBuilder.Append(string.Format("{0}=", valueName));

                if (objectValues.Count > 0)
                {
                    structStringBuilder.Append("(");

                    for (int i = 0; i < objectValues.Count; i++)
                    {
                        KeyValuePair<string, object> valuePair = objectValues.ElementAt(i);

                        string subName = valuePair.Key;
                        object subValue = valuePair.Value;

                        string stringValue = ObjectToString(sectionName, subName, subValue, true);

                        if (stringValue.Length > 0)
                        {
                            structStringBuilder.Append(stringValue);

                            bool isLastEntry = i == (objectValues.Count - 1);
                            if (!isLastEntry)
                                structStringBuilder.Append(",");
                        }
                    }

                    structStringBuilder.Append(")");
                }

                return structStringBuilder.ToString();
            }
            else if (value.GetType() == typeof(List<object>))
            {
                List<object> arrayValues = value as List<object>;
                StringBuilder arrayStringBuilder = new StringBuilder();

                if (arrayValues.Count > 0)
                {
                    int spaceIndex = sectionName.IndexOf(" ");
                    bool isDataObject = spaceIndex != -1;
                    bool hasPredefinedArrayLength;

                    if (isDataObject)
                    {

                        string dataObjectType = sectionName.Substring(spaceIndex + 1);

                        hasPredefinedArrayLength = dataObjectPredefinedArrayLengthKeys.ContainsKey(dataObjectType) &&
                                                    dataObjectPredefinedArrayLengthKeys[dataObjectType].Contains(valueName);
                    }
                    else
                    {
                        hasPredefinedArrayLength = sectionPredefinedArrayLengthKeys.ContainsKey(sectionName) &&
                                                    sectionPredefinedArrayLengthKeys[sectionName].Contains(valueName); // todo store in expando object?
                    }

                    for (int i = 0; i < arrayValues.Count; i++)
                    {
                        string arrayEntryName = hasPredefinedArrayLength ? string.Format("{0}[{1}]", valueName, i) : valueName;
                        string valueString = ObjectToString(sectionName, arrayEntryName, arrayValues[i], isNestedValue);

                        if (valueString.Length > 0)
                        {
                            if (arrayStringBuilder.Length > 0)
                            {
                                if (isNestedValue)
                                    arrayStringBuilder.Append(",");
                                else arrayStringBuilder.Append(Environment.NewLine);
                            }

                            arrayStringBuilder.Append(valueString);
                        }
                    }
                }

                return arrayStringBuilder.ToString();
            }
            else if (value.GetType() == typeof(string))
            {
                string text = value as string;

                bool isClassReference = text.StartsWith("Class'");
                if (isClassReference)
                {
                    return string.Format("{0}={1}", valueName, value);
                }
                else if (isNestedValue)
                {
                    if (text.Length > 0)
                        return string.Format("{0}=\"{1}\"", valueName, value);
                    else return string.Format("{0}=", valueName);
                }
                else
                {
                    return string.Format("{0}={1}", valueName, value);
                }
            }
            else if (value.GetType() == typeof(float))
            {
                return string.Format("{0}={1}", valueName, ((float)value).ToString("0.000000", CultureInfo.InvariantCulture));
            }
            else
            {
                return string.Format("{0}={1}", valueName, value);
            }
        }
    }
}

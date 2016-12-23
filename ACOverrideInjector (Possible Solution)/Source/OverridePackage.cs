using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Verse;

namespace AlcoholV
{
    public class OverridePackage
    {
        private const BindingFlags FieldBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Dictionary<string, string> injections = new Dictionary<string, string>();

        public Type defType;

        public OverridePackage(Type defType)
        {
            this.defType = defType;
        }

        private string ProcessedPath(string path)
        {
            if (!path.Contains('[') && !path.Contains(']'))
            {
                return path;
            }
            return path.Replace("]", string.Empty).Replace('[', '.');
        }

        private string ProcessedTranslation(string rawTranslation)
        {
            return rawTranslation.Replace("\\n", "\n");
        }

        public void AddDataFromFile(FileInfo file)
        {
            try
            {
                var xDocument = XDocument.Load(file.FullName);
                foreach (var current in xDocument.Root.Elements())
                {
                    if (current.Name == "rep")
                    {
                        var key = ProcessedPath(current.Elements("path").First().Value);
                        var translation = ProcessedTranslation(current.Elements("trans").First().Value);
                        TryAddInjection(file, key, translation);
                    }
                    else
                    {
                        var key2 = ProcessedPath(current.Name.ToString());
                        var translation2 = ProcessedTranslation(current.Value);
                        TryAddInjection(file, key2, translation2);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(string.Concat("Exception loading override data from file ", file, ": ", ex));
            }
        }

        private void TryAddInjection(FileInfo file, string key, string translation)
        {
            if (HasError(file, key))
            {
                return;
            }
            injections.Add(key, translation);
        }

        private bool HasError(FileInfo file, string key)
        {
            if (!key.Contains('.'))
            {
                Log.Warning(string.Concat("Error loading DefInjection from file ", file, ": Key lacks a dot: ", key));
                return true;
            }
            if (injections.ContainsKey(key))
            {
                Log.Warning("Duplicate def-linked override key: " + key);
                return true;
            }
            return false;
        }

        public void InjectIntoDefs()
        {
            foreach (var current in injections)
            {
                var array = current.Key.Split('.');
                var text = array[0];
                text = BackCompatibility.BackCompatibleDefName(defType, text);
                if (GenGeneric.InvokeStaticMethodOnGenericType(typeof (DefDatabase<>), defType, "GetNamedSilentFail", text) == null)
                {
                    Log.Warning(string.Concat("Def-linked override error: Found no ", defType, " named ", text, " to match ", current.Key));
                }
                else
                {
                    SetDefFieldAtPath(defType, current.Key, current.Value);
                }
            }
            GenGeneric.InvokeStaticMethodOnGenericType(typeof (DefDatabase<>), defType, "ClearCachedData");
        }

        private void SetDefFieldAtPath(Type defType, string path, string value)
        {
            path = BackCompatibility.BackCompatibleModifiedTranslationPath(defType, path);
            try
            {
                var list = path.Split('.').ToList();
                var obj = GenGeneric.InvokeStaticMethodOnGenericType(typeof (DefDatabase<>), defType, "GetNamedSilentFail", list[0]);
                if (obj == null)
                {
                    throw new InvalidOperationException("Def named " + list[0] + " not found.");
                }
                list.RemoveAt(0);
                DefInjectionPathPartKind defInjectionPathPartKind;
                string text;
                int num;
                while (true)
                {
                    defInjectionPathPartKind = DefInjectionPathPartKind.Field;
                    text = list[0];
                    num = -1;
                    if (text.Contains('['))
                    {
                        defInjectionPathPartKind = DefInjectionPathPartKind.FieldWithListIndex;
                        var array = text.Split('[');
                        var text2 = array[1];
                        text2 = text2.Substring(0, text2.Length - 1);
                        num = (int) ParseHelper.FromString(text2, typeof (int));
                        text = array[0];
                    }
                    else if (int.TryParse(text, out num))
                    {
                        defInjectionPathPartKind = DefInjectionPathPartKind.ListIndex;
                    }
                    if (list.Count == 1)
                    {
                        break;
                    }
                    if (defInjectionPathPartKind == DefInjectionPathPartKind.ListIndex)
                    {
                        var property = obj.GetType().GetProperty("Item");
                        if (property == null)
                        {
                            goto Block_17;
                        }

                        obj = property.GetValue(obj, new object[] {num});
                    }
                    else
                    {
                        var field = obj.GetType().GetField(text, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field == null)
                        {
                            goto Block_18;
                        }
                        if (defInjectionPathPartKind == DefInjectionPathPartKind.Field)
                        {
                            //obj = field.GetValue(obj);
                            // check struct
                            var t = field.GetValue(obj);
                            if (t.GetType().IsValueType)
                            {
                                break;
                            }
                            obj = t;
                        }
                        else
                        {
                            var value2 = field.GetValue(obj);
                            var property2 = value2.GetType().GetProperty("Item");

                            if (property2 == null)
                            {
                                goto Block_20;
                            }
                            obj = property2.GetValue(value2, new object[] {num});
                        }
                    }
                    list.RemoveAt(0);
                }


                if (defInjectionPathPartKind == DefInjectionPathPartKind.Field)
                {
                    var fieldNamed = GetFieldNamed(obj.GetType(), text);
                    if (fieldNamed == null)
                    {
                        throw new InvalidOperationException("Field " + text + " does not exist.");
                    }
                    if (fieldNamed.HasAttribute<NoTranslateAttribute>())
                    {
                        Log.Error(string.Concat("override untranslateable field ", fieldNamed.Name, " of type ", fieldNamed.FieldType, " at path ", path, ". Translating this field will break the game."));
                    }
                    else
                    {
                        //fieldNamed.SetValueDirect(__makeref(obj), Convert.ChangeType(value, fieldNamed.FieldType));

                        // object is struct
                        if (list.Count > 1)
                        {
                            // create instance
                            var o = FormatterServices.GetUninitializedObject(fieldNamed.FieldType);
                            StructCopy(o.GetType(), fieldNamed.GetValue(obj), o); // copy

                            var field = GetFieldNamed(o.GetType(), list[1]);
                            field.SetValue(o, Convert.ChangeType(value, field.FieldType));
                            fieldNamed.SetValue(obj, o);
                        }
                        else
                        {
                            fieldNamed.SetValue(obj, Convert.ChangeType(value, fieldNamed.FieldType));
                        }
                    }
                }
                else
                {
                    object obj2;
                    if (defInjectionPathPartKind == DefInjectionPathPartKind.FieldWithListIndex)
                    {
                        var field2 = obj.GetType().GetField(text, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field2 == null)
                        {
                            throw new InvalidOperationException("Field " + text + " does not exist.");
                        }
                        obj2 = field2.GetValue(obj);
                    }
                    else
                    {
                        obj2 = obj;
                    }
                    var type = obj2.GetType();
                    var property3 = type.GetProperty("Count");
                    if (property3 == null)
                    {
                        throw new InvalidOperationException("Tried to use index on non-list (missing 'Count' property).");
                    }
                    var num2 = (int) property3.GetValue(obj2, null);
                    if (num >= num2)
                    {
                        throw new InvalidOperationException(string.Concat("Trying to override ", defType, ".", path, " at index ", num, " but the original list only has ", num2, " entries (so the max index is ", (num2 - 1).ToString(), ")."));
                    }
                    var property4 = type.GetProperty("Item");
                    if (property4 == null)
                    {
                        throw new InvalidOperationException("Tried to use index on non-list (missing 'Item' property).");
                    }
                    property4.SetValue(obj2, value, new object[]
                    {
                        num
                    });
                }
                return;
                Block_17:
                throw new InvalidOperationException("Tried to use index on non-list (missing 'Item' property).");
                Block_18:
                throw new InvalidOperationException("Field " + text + " does not exist.");
                Block_20:
                throw new InvalidOperationException("Tried to use index on non-list (missing 'Item' property).");
            }
            catch (Exception ex)
            {
                Log.Warning(string.Concat("Def-linked override error: Exception getting field at path ", path, " in ", defType, ": ", ex.ToString()));
            }
        }

        private FieldInfo GetFieldNamed(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                var fields = type.GetFields(BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (var i = 0; i < fields.Length; i++)
                {
                    var customAttributes = fields[i].GetCustomAttributes(typeof (LoadAliasAttribute), false);
                    if (customAttributes != null && customAttributes.Length > 0)
                    {
                        for (var j = 0; j < customAttributes.Length; j++)
                        {
                            var loadAliasAttribute = (LoadAliasAttribute) customAttributes[j];
                            if (loadAliasAttribute.alias == name)
                            {
                                return fields[i];
                            }
                        }
                    }
                }
            }
            return field;
        }


        private static void StructCopy(Type type, object source, object destination)
        {
            var myObjectFields = type.GetFields(BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var fi in myObjectFields)
            {
                fi.SetValue(destination, fi.GetValue(source));
            }
        }

        internal enum DefInjectionPathPartKind
        {
            Field,
            FieldWithListIndex,
            ListIndex
        }
    }
}
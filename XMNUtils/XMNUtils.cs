using System;
using System.Linq;
using JetBrains.Annotations;
using VoxelTycoon.AssetManagement;

namespace XMNUtils
{
    using UnityEngine;
    using System.IO;
    using VoxelTycoon.Tracks;
    using VoxelTycoon.UI;
    using System.Collections.Generic;

    internal static class GameObjectDumper
    {
        static private StringWriter _stringWriter;

        static private StringWriter StringWriter {
            get {
                StringWriter result;
                if ((result = _stringWriter) == null)
                {
                    result = (_stringWriter = new StringWriter());
                }
                return result;
            }
        }

        public static string DumpGameObject(GameObject gameObject)
        {
            StringWriter stringWriter = StringWriter;
            stringWriter.GetStringBuilder().Clear();
            DumpGameObjectInternal(gameObject, stringWriter);
            return stringWriter.ToString();
        }

        public static void DumpGameObject(GameObject gameObject, TextWriter writer)
        {
            DumpGameObjectInternal(gameObject, writer);
        }

        private static void DumpGameObjectInternal(GameObject gameObject, TextWriter writer, string indent = "  ")
        {
            writer.WriteLine("{0}+{1} ({2})", indent, gameObject.name, gameObject.transform.GetType().Name);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                DumpComponent(component, writer, indent + "  ");
            }

            foreach (Transform child in gameObject.transform)
            {
                DumpGameObjectInternal(child.gameObject, writer, indent + "  ");
            }
        }

        private static void DumpComponent(Component component, TextWriter writer, string indent)
        {
            writer.WriteLine("{0}{1}", indent, (component == null ? "(null)" : component.GetType().Name));
        }
    }

    public static class DictionaryUtils
    {
        public static float AddFloatToDict<T>(this Dictionary<T, float> dictionary, T key, float count)
        {
            if (!dictionary.TryGetValue(key, out float dictCount))
            {
                dictionary.Add(key, count);
                return count;
            }
            float result = dictionary[key] = dictCount + count;
            return result;
        }
        public static void AddIntToDict<T>(this Dictionary<T, int> dictionary, T key, int count)
        {
            if (!dictionary.TryGetValue(key, out int dictCount))
            {
                dictionary.Add(key, count);
            }
            else
            {
                dictionary[key] = dictCount + count;
            }
        }

        public static void SubIntFromDict<T>(this Dictionary<T, int> dictionary, T key, int count, int minLimit=int.MinValue)
        {
            if (!dictionary.TryGetValue(key, out int dictCount))
                throw new InvalidOperationException("No data tu subtract from.");

            if (dictCount < minLimit + count)
                throw new InvalidOperationException("Value underflow");
            dictionary[key] = dictCount - count;
        }

        /** return true when the resulting value is a zero */
        public static bool SubIntFromDictTestZero<T>(this Dictionary<T, int> dictionary, T key, int count, int minLimit=int.MinValue)
        {
            if (!dictionary.TryGetValue(key, out int dictCount))
                throw new InvalidOperationException("No data tu subtract from.");

            if (dictCount < minLimit + count)
                throw new InvalidOperationException("Value underflow");

            int result = dictCount - count;
            dictionary[key] = result;
            return result == 0;
        }
        
        public static bool TrySubIntFromDict<T>(this Dictionary<T, int> dictionary, T key, int count, int minLimit=int.MinValue, bool removeWhenZero = false)
        {
            if (!dictionary.TryGetValue(key, out int dictCount))
                return false;

            if (dictCount < minLimit + count)
                return false;
            if (removeWhenZero && dictCount - count == 0)
            {
                dictionary.Remove(key);
                return true;
            }
            dictionary[key] = dictCount - count;
            return true;
        }
    }

    public static class NotificationUtils
    {
        public static void ShowVehicleHint(Vehicle vehicle, string message, Color? textColor=null, Color? panelColor=null)
        {
            FloatingHint.ShowHint(message, vehicle.HeadPosition.GetValueOrDefault(), textColor.GetValueOrDefault(Color.white), new PanelColor?(new PanelColor(panelColor.GetValueOrDefault(Color.black), 0.4f)));
        }
    }

    public class Version: IComparable<Version>, IComparable
    {
        private int[] parsed;

        private static int[] ParseVersion(string versionString)
        {
            string[] parsedStr = versionString.Split('.');
            int[] result = new int[parsedStr.Length];
            for (var index = 0; index < parsedStr.Length; index++)
            {
                result[index] = int.Parse(parsedStr[index]);
            }

            return result;
        }
        
        public Version(string versionString)
        {
            parsed = ParseVersion(versionString);
        }

        public Version()
        {
            parsed = ParseVersion(GetApplicationVersion());
        }

        public static string GetApplicationVersion()
        {
            return Application.version.Split(' ')[0];
        }

        public int CompareTo(string version)
        {
            return CompareTo(new Version(version));
        }

        public int CompareTo(Version other)
        {
            for (int i = 0; i < parsed.Length; i++)
            {
                if (other.parsed.Length <= i)
                {
                    return 1;
                }

                int diff = parsed[i] - other.parsed[i];
                if (diff != 0)
                {
                    return diff;
                }
            }

            if (other.parsed.Length > parsed.Length)
            {
                return -1;
            }

            return 0;
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Version other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Version)}");
        }
    }

    internal static class ModFunctions
    {
        public static bool IsModInstalled(string modNamespace)
        {
            return EnabledPacksPerSaveHelper.GetEnabledPackIds().Any(pack => pack.Name == modNamespace);
        }
    }

    public static class ExceptionFault
    {
        public static bool FaultBlock(Exception e, Action<Exception> action)
        {
            action?.Invoke(e);
            return false;
        }
    }
}

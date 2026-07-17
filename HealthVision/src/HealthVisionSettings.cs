using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace HealthVision
{
    internal static class HealthVisionSettings
    {
        private const string Tag = "[HealthVision]";

        internal static float Range { get; private set; } = 40f;
        internal static bool ShowNumbers { get; private set; } = true;
        internal static float BarWidth { get; private set; } = 60f;
        internal static float BarHeight { get; private set; } = 24f;
        internal static int FontSize { get; private set; } = 28;
        internal static bool ShowPlayers { get; private set; } = true;

        internal static void Load(string modPath)
        {
            string root = string.IsNullOrEmpty(modPath) ? Directory.GetCurrentDirectory() : modPath;
            string configPath = Path.Combine(root, "Config", "HealthVision.xml");
            if (!File.Exists(configPath))
            {
                Log.Warning(Tag + " Config missing, using defaults: " + configPath);
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                Range = Mathf.Max(1f, ReadFloat(doc, "Range", Range));
                ShowNumbers = ReadBool(doc, "ShowNumbers", ShowNumbers);
                BarWidth = Mathf.Max(8f, ReadFloat(doc, "BarWidth", BarWidth));
                BarHeight = Mathf.Max(2f, ReadFloat(doc, "BarHeight", BarHeight));
                FontSize = Mathf.Clamp(ReadInt(doc, "FontSize", FontSize), 6, 96);
                ShowPlayers = ReadBool(doc, "ShowPlayers", ShowPlayers);
            }
            catch (Exception ex)
            {
                Log.Error(Tag + " Failed to load config " + configPath + ": " + ex);
            }
        }

        private static string ReadString(XmlDocument doc, string name, string fallback)
        {
            XmlNode node = doc.SelectSingleNode("/config/" + name);
            if (node == null)
                return fallback;

            XmlAttribute attr = node.Attributes?["value"];
            string value = attr != null ? attr.Value : node.InnerText;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static float ReadFloat(XmlDocument doc, string name, float fallback)
        {
            string value = ReadString(doc, name, "");
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;

            return fallback;
        }

        private static int ReadInt(XmlDocument doc, string name, int fallback)
        {
            string value = ReadString(doc, name, "");
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;

            return fallback;
        }

        private static bool ReadBool(XmlDocument doc, string name, bool fallback)
        {
            string value = ReadString(doc, name, "");
            if (bool.TryParse(value, out bool parsed))
                return parsed;

            if (value == "1") return true;
            if (value == "0") return false;
            return fallback;
        }
    }
}

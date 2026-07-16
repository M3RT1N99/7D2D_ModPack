using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace DeathSound
{
    internal static class DeathSoundSettings
    {
        private const string Tag = "[DeathSound]";

        internal static string ModPath { get; private set; } = "";
        internal static string AudioFile { get; private set; } = "Audio/death.mp3";
        internal static float Volume { get; private set; } = 0.85f;
        internal static bool PlayForRemotePlayers { get; private set; } = true;
        internal static float CooldownSeconds { get; private set; } = 1.0f;

        internal static string AudioPath
        {
            get
            {
                if (Path.IsPathRooted(AudioFile))
                    return AudioFile;

                return Path.GetFullPath(Path.Combine(ModPath, AudioFile));
            }
        }

        internal static void Load(string modPath)
        {
            ModPath = string.IsNullOrEmpty(modPath) ? Directory.GetCurrentDirectory() : modPath;

            string configPath = Path.Combine(ModPath, "Config", "DeathSound.xml");
            if (!File.Exists(configPath))
            {
                Log.Warning(Tag + " Config missing, using defaults: " + configPath);
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                AudioFile = ReadString(doc, "AudioFile", AudioFile);
                Volume = Mathf.Clamp01(ReadFloat(doc, "Volume", Volume));
                PlayForRemotePlayers = ReadBool(doc, "PlayForRemotePlayers", PlayForRemotePlayers);
                CooldownSeconds = Mathf.Max(0f, ReadFloat(doc, "CooldownSeconds", CooldownSeconds));
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

        private static bool ReadBool(XmlDocument doc, string name, bool fallback)
        {
            string value = ReadString(doc, name, "");
            if (bool.TryParse(value, out bool parsed))
                return parsed;

            if (value == "1")
                return true;

            if (value == "0")
                return false;

            return fallback;
        }
    }
}

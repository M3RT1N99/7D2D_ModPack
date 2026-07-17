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

        // Supported audio extensions (mirrors DeathSoundPlayer.GetAudioType).
        private static readonly string[] AudioExtensions = { ".mp3", ".ogg", ".wav", ".aif", ".aiff" };

        internal static string ModPath { get; private set; } = "";

        // Empty => auto-detect the first supported file in the mod's Audio folder.
        internal static string AudioFile { get; private set; } = "";
        internal static float Volume { get; private set; } = 0.85f;
        internal static bool PlayForRemotePlayers { get; private set; } = true;
        internal static float CooldownSeconds { get; private set; } = 1.0f;

        // --- Explosion on player death (server-authoritative world event) ---
        // Two flat damage tiers applied as a percentage of each victim's max health:
        // an inner "fireball" core and an outer "shockwave" ring.
        internal static bool ExplosionEnabled { get; private set; } = true;
        internal static float ExplosionDelaySeconds { get; private set; } = 2.0f;
        internal static float ExplosionInnerRadius { get; private set; } = 50f;
        internal static float ExplosionInnerDamagePercent { get; private set; } = 95f;
        internal static float ExplosionOuterRadius { get; private set; } = 150f;
        internal static float ExplosionOuterDamagePercent { get; private set; } = 50f;
        // Block destruction is a single crater (the tier model applies to entities only).
        internal static float ExplosionBlockDamage { get; private set; } = 2500f;
        internal static float ExplosionBlockRadius { get; private set; } = 5f;
        internal static int ExplosionBlastPower { get; private set; } = 100;
        internal static int ExplosionParticleIndex { get; private set; } = 5;

        private static string resolvedAudioPath;

        internal static string AudioPath
        {
            get
            {
                if (string.IsNullOrEmpty(resolvedAudioPath))
                    resolvedAudioPath = ResolveAudioPath();

                return resolvedAudioPath;
            }
        }

        internal static void Load(string modPath)
        {
            ModPath = string.IsNullOrEmpty(modPath) ? Directory.GetCurrentDirectory() : modPath;

            string configPath = Path.Combine(ModPath, "Config", "DeathSound.xml");
            if (File.Exists(configPath))
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(configPath);

                    AudioFile = ReadString(doc, "AudioFile", AudioFile);
                    Volume = Mathf.Clamp01(ReadFloat(doc, "Volume", Volume));
                    PlayForRemotePlayers = ReadBool(doc, "PlayForRemotePlayers", PlayForRemotePlayers);
                    CooldownSeconds = Mathf.Max(0f, ReadFloat(doc, "CooldownSeconds", CooldownSeconds));

                    ExplosionEnabled = ReadBool(doc, "ExplosionEnabled", ExplosionEnabled);
                    ExplosionDelaySeconds = Mathf.Max(0f, ReadFloat(doc, "ExplosionDelaySeconds", ExplosionDelaySeconds));
                    ExplosionInnerRadius = Mathf.Max(0f, ReadFloat(doc, "ExplosionInnerRadius", ExplosionInnerRadius));
                    ExplosionInnerDamagePercent = Mathf.Max(0f, ReadFloat(doc, "ExplosionInnerDamagePercent", ExplosionInnerDamagePercent));
                    ExplosionOuterRadius = Mathf.Max(0f, ReadFloat(doc, "ExplosionOuterRadius", ExplosionOuterRadius));
                    ExplosionOuterDamagePercent = Mathf.Max(0f, ReadFloat(doc, "ExplosionOuterDamagePercent", ExplosionOuterDamagePercent));
                    ExplosionBlockDamage = Mathf.Max(0f, ReadFloat(doc, "ExplosionBlockDamage", ExplosionBlockDamage));
                    ExplosionBlockRadius = Mathf.Max(0f, ReadFloat(doc, "ExplosionBlockRadius", ExplosionBlockRadius));
                    // BlastPower is capped at ExplosionData.cMaxBlastPower (100); ParticleIndex
                    // must index WorldStaticData.prefabExplosions (0..99).
                    ExplosionBlastPower = Mathf.Clamp(ReadInt(doc, "ExplosionBlastPower", ExplosionBlastPower), 0, 100);
                    ExplosionParticleIndex = Mathf.Clamp(ReadInt(doc, "ExplosionParticleIndex", ExplosionParticleIndex), 0, 99);
                }
                catch (Exception ex)
                {
                    Log.Error(Tag + " Failed to load config " + configPath + ": " + ex);
                }
            }
            else
            {
                Log.Warning(Tag + " Config missing, using defaults: " + configPath);
            }

            resolvedAudioPath = ResolveAudioPath();
            Log.Out(Tag + " Using audio file: " + resolvedAudioPath);
        }

        // An explicit AudioFile (relative to the mod, or absolute) always wins.
        // Otherwise the first supported file in the Audio folder is used automatically.
        private static string ResolveAudioPath()
        {
            if (!string.IsNullOrWhiteSpace(AudioFile))
            {
                return Path.IsPathRooted(AudioFile)
                    ? AudioFile
                    : Path.GetFullPath(Path.Combine(ModPath, AudioFile));
            }

            string detected = DetectAudioFile();
            if (!string.IsNullOrEmpty(detected))
                return detected;

            // Nothing found; return a conventional default (caller logs / plays fallback tone).
            return Path.GetFullPath(Path.Combine(ModPath, "Audio", "death.wav"));
        }

        private static string DetectAudioFile()
        {
            string audioDir = Path.Combine(ModPath, "Audio");
            if (!Directory.Exists(audioDir))
                return null;

            string best = null;
            foreach (string file in Directory.GetFiles(audioDir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(AudioExtensions, ext) < 0)
                    continue;

                // Prefer a file literally named "death.*"; otherwise pick the
                // alphabetically first supported file for deterministic behaviour.
                if (string.Equals(Path.GetFileNameWithoutExtension(file), "death", StringComparison.OrdinalIgnoreCase))
                    return file;

                if (best == null || string.CompareOrdinal(file, best) < 0)
                    best = file;
            }

            return best;
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

            if (value == "1")
                return true;

            if (value == "0")
                return false;

            return fallback;
        }
    }
}

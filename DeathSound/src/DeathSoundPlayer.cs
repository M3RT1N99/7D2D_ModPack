using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DeathSound
{
    internal sealed class DeathSoundPlayer : MonoBehaviour
    {
        private const string Tag = "[DeathSound]";
        internal static DeathSoundPlayer instance;

        private AudioSource source;
        private AudioClip cachedClip;
        private string cachedPath;
        private float nextAllowedTime;
        private bool loadFailed;

        internal static void Ensure()
        {
            if (instance != null)
                return;

            GameObject go = new GameObject("DeathSoundPlayer");
            DontDestroyOnLoad(go);

            instance = go.AddComponent<DeathSoundPlayer>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
        }

        internal static void Play(string playerName)
        {
            Ensure();
            instance.StartCoroutine(instance.PlayRoutine(playerName));
        }

        private IEnumerator PlayRoutine(string playerName)
        {
            if (Time.unscaledTime < nextAllowedTime)
                yield break;

            nextAllowedTime = Time.unscaledTime + DeathSoundSettings.CooldownSeconds;

            string path = DeathSoundSettings.AudioPath;
            if (!File.Exists(path))
            {
                Log.Warning(Tag + " Audio file not found, playing generated test tone: " + path);
                PlayFallbackTone();
                yield break;
            }

            if (cachedClip == null || cachedPath != path)
                yield return LoadClip(path);

            if (cachedClip == null)
            {
                PlayFallbackTone();
                yield break;
            }

            Log.Out(Tag + " Player death detected: " + playerName);
            source.PlayOneShot(cachedClip, DeathSoundSettings.Volume);
        }

        private IEnumerator LoadClip(string path)
        {
            if (loadFailed && cachedPath == path)
                yield break;

            cachedPath = path;
            loadFailed = false;

            AudioType audioType = GetAudioType(path);
            string uri = new Uri(path).AbsoluteUri;

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    loadFailed = true;
                    cachedClip = null;
                    Log.Error(Tag + " Failed to load audio " + path + ": " + req.error);
                    yield break;
                }

                cachedClip = DownloadHandlerAudioClip.GetContent(req);
                if (cachedClip == null)
                {
                    loadFailed = true;
                    Log.Error(Tag + " Unity returned no AudioClip for " + path);
                }
            }
        }

        private static AudioType GetAudioType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                case ".aif":
                case ".aiff":
                    return AudioType.AIFF;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private void PlayFallbackTone()
        {
            const int sampleRate = 44100;
            const float duration = 0.75f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            double phase = 0d;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = t / duration;
                float frequency = Mathf.Lerp(660f, 220f, normalized);
                float envelope = Mathf.Clamp01(1f - normalized);

                phase += 2d * Math.PI * frequency / sampleRate;
                samples[i] = (float)Math.Sin(phase) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("DeathSoundFallback", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            source.PlayOneShot(clip, DeathSoundSettings.Volume);
        }
    }
}
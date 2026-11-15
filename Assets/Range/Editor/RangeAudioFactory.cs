using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShootingRange.EditorTools
{
    /// <summary>Synthesises the range's ambience (ventilation hum) as a seamless loop WAV.</summary>
    internal static class RangeAudioFactory
    {
        public static AudioClip GetOrCreateRoomTone(string path, bool force)
        {
            if (force || !File.Exists(path))
            {
                const int rate = 44100;
                const float seconds = 12f;
                var count = (int)(rate * seconds);
                var samples = new float[count];
                var rng = new System.Random(1234);

                // Brown-ish noise (air handling) + a soft 60 Hz fan hum with slow wobble.
                float brown = 0f, low = 0f;
                for (var i = 0; i < count; i++)
                {
                    var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
                    low += (brown - low) * 0.08f;
                    var t = i / (float)rate;
                    var hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.05f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.02f;
                    var wobble = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / seconds * 3f);
                    samples[i] = (low * 0.9f + hum) * wobble;
                }

                // Crossfade the tail into the head so the loop is seamless.
                var fade = rate / 2;
                for (var i = 0; i < fade; i++)
                {
                    var a = i / (float)fade;
                    samples[i] = samples[i] * a + samples[count - fade + i] * (1f - a);
                }

                Array.Resize(ref samples, count - fade);
                Normalise(samples, 0.6f);
                WriteWav(path, samples, rate);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void Normalise(float[] samples, float peak)
        {
            var max = 0f;
            foreach (var s in samples) max = Mathf.Max(max, Mathf.Abs(s));
            if (max <= 0f) return;
            var k = peak / max;
            for (var i = 0; i < samples.Length; i++) samples[i] *= k;
        }

        private static void WriteWav(string path, float[] samples, int rate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            var dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}

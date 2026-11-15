using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShootingRange.EditorTools
{
    /// <summary>One texel of a procedural surface.</summary>
    internal struct Surface
    {
        public Color Albedo;
        public float Height;    // 0..1, used to derive the normal map
        public float Smoothness;
        public float Metallic;
        public float Occlusion; // 1 = unoccluded

        public Surface(Color albedo, float height, float smoothness, float metallic = 0f, float occlusion = 1f)
        {
            Albedo = albedo;
            Height = height;
            Smoothness = smoothness;
            Metallic = metallic;
            Occlusion = occlusion;
        }
    }

    internal sealed class TextureSet
    {
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D Mask; // R metallic, G occlusion, A smoothness
        public float WorldSize; // metres covered by one tile
    }

    /// <summary>
    /// Bakes tileable albedo / normal / mask textures from a procedural sampler and imports them.
    /// </summary>
    internal static class RangeTextureFactory
    {
        public delegate Surface Sampler(float u, float v);

        public static TextureSet GetOrCreate(string folder, string name, int size, float worldSize,
            float normalStrength, Sampler sampler, bool force)
        {
            var albedoPath = $"{folder}/{name}_Albedo.png";
            var normalPath = $"{folder}/{name}_Normal.png";
            var maskPath = $"{folder}/{name}_Mask.png";

            if (force || !File.Exists(albedoPath) || !File.Exists(normalPath) || !File.Exists(maskPath))
                Bake(size, normalStrength, sampler, albedoPath, normalPath, maskPath);

            return new TextureSet
            {
                Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                Mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath),
                WorldSize = worldSize
            };
        }

        private static void Bake(int size, float normalStrength, Sampler sampler,
            string albedoPath, string normalPath, string maskPath)
        {
            var count = size * size;
            var albedo = new Color[count];
            var mask = new Color[count];
            var height = new float[count];

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var s = sampler((x + 0.5f) / size, (y + 0.5f) / size);
                var i = y * size + x;
                var a = s.Albedo;
                a.a = 1f;
                albedo[i] = a;
                height[i] = s.Height;
                mask[i] = new Color(s.Metallic, s.Occlusion, 0f, s.Smoothness);
            }

            var normal = new Color[count];
            var k = normalStrength * size / 512f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var l = height[y * size + (x - 1 + size) % size];
                var r = height[y * size + (x + 1) % size];
                var d = height[((y - 1 + size) % size) * size + x];
                var u = height[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * k, (d - u) * k, 1f).normalized;
                normal[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }

            Write(albedoPath, size, albedo);
            Write(normalPath, size, normal);
            Write(maskPath, size, mask);

            Import(albedoPath, srgb: true, normalMap: false);
            Import(normalPath, srgb: false, normalMap: true);
            Import(maskPath, srgb: false, normalMap: false);
        }

        private static void Write(string path, int size, Color[] pixels)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void Import(string path, bool srgb, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- noise

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Tileable value noise with separate periods per axis.</summary>
        public static float Value(float u, float v, int periodX, int periodY, int seed)
        {
            var x = u * periodX;
            var y = v * periodY;
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = Smooth(x - x0);
            var fy = Smooth(y - y0);
            int ax = Mod(x0, periodX), bx = Mod(x0 + 1, periodX);
            int ay = Mod(y0, periodY), by = Mod(y0 + 1, periodY);
            var a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
            var b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        public static float Value(float u, float v, int period, int seed) => Value(u, v, period, period, seed);

        /// <summary>Tileable fractal noise, roughly 0..1.</summary>
        public static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += amp * Value(u, v, periodX << i, periodY << i, seed + i * 31);
                norm += amp;
                amp *= gain;
            }

            return sum / norm;
        }

        public static float Fbm(float u, float v, int period, int octaves, int seed, float gain = 0.5f) =>
            Fbm(u, v, period, period, octaves, seed, gain);

        /// <summary>Tileable cellular noise: distance to the nearest feature point (in cell units) and cell id.</summary>
        public static float Worley(float u, float v, int period, int seed, out float cellId)
        {
            var x = u * period;
            var y = v * period;
            var cx = Mathf.FloorToInt(x);
            var cy = Mathf.FloorToInt(y);
            var best = 10f;
            cellId = 0f;
            for (var j = -1; j <= 1; j++)
            for (var i = -1; i <= 1; i++)
            {
                var gx = cx + i;
                var gy = cy + j;
                int wx = Mod(gx, period), wy = Mod(gy, period);
                var px = gx + Hash(wx, wy, seed);
                var py = gy + Hash(wx, wy, seed + 7);
                var d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < best)
                {
                    best = d;
                    cellId = Hash(wx, wy, seed + 13);
                }
            }

            return best;
        }

        public static float Frac(float x) => x - Mathf.Floor(x);
    }
}

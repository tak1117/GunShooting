using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShootingRange.EditorTools.RangeTextureFactory;

namespace ShootingRange.EditorTools
{
    /// <summary>
    /// Procedural surfaces and the URP Lit materials built from them.
    /// Every texture is generated here; nothing comes from third-party packs.
    /// </summary>
    internal sealed class RangeMaterials
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        private static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private readonly string _materialFolder;
        private readonly Dictionary<string, TextureSet> _textures = new Dictionary<string, TextureSet>();

        public Material ConcreteFloor, ConcreteDownrange, BlockWall, BlockWallDark, AcousticNavy, AcousticGraphite,
            RubberBerm, SteelBrushed, SteelDark, PaintedWhite, PaintedYellow, PaintedRed, PaintedGrey, PaintedBlueGrey,
            WoodCounter, CeilingDeck, CeilingTile, Hazard, RubberMat, Pegboard, Glass, ScreenGlass, PlasticBlack,
            Cardboard, LedWhite, LedWarm, ExitGreen, BeaconRed, BeaconGreen, FloorLineYellow, FloorLineWhite,
            FloorLineRed, Fixture;

        public RangeMaterials(string textureFolder, string materialFolder, bool forceTextures)
        {
            _materialFolder = materialFolder;

            Tex("ConcreteFloor", 1024, 4f, 1.2f, ConcreteFloorSurface, textureFolder, forceTextures);
            Tex("ConcreteDownrange", 1024, 4f, 1.4f, ConcreteDownrangeSurface, textureFolder, forceTextures);
            Tex("BlockWall", 1024, 2f, 2.5f, BlockWallSurface, textureFolder, forceTextures);
            Tex("AcousticFabric", 512, 1f, 0.8f, AcousticSurface, textureFolder, forceTextures);
            Tex("RubberBerm", 512, 2f, 2.0f, RubberBermSurface, textureFolder, forceTextures);
            Tex("SteelBrushed", 512, 1f, 0.4f, SteelBrushedSurface, textureFolder, forceTextures);
            Tex("PaintedMetal", 512, 1f, 0.35f, PaintedMetalSurface, textureFolder, forceTextures);
            Tex("WoodLaminate", 512, 1.2f, 0.4f, WoodSurface, textureFolder, forceTextures);
            Tex("CeilingDeck", 512, 1.2f, 3.0f, CeilingDeckSurface, textureFolder, forceTextures);
            Tex("CeilingTile", 512, 1.2f, 1.5f, CeilingTileSurface, textureFolder, forceTextures);
            Tex("Hazard", 512, 0.6f, 0.5f, HazardSurface, textureFolder, forceTextures);
            Tex("RubberMat", 512, 0.5f, 2.0f, RubberMatSurface, textureFolder, forceTextures);
            Tex("Pegboard", 512, 0.5f, 2.0f, PegboardSurface, textureFolder, forceTextures);
            Tex("Cardboard", 256, 0.5f, 0.6f, CardboardSurface, textureFolder, forceTextures);

            ConcreteFloor = Lit("M_ConcreteFloor", "ConcreteFloor", Color.white);
            ConcreteDownrange = Lit("M_ConcreteDownrange", "ConcreteDownrange", Color.white);
            BlockWall = Lit("M_BlockWall", "BlockWall", new Color(0.93f, 0.92f, 0.89f));
            BlockWallDark = Lit("M_BlockWallDark", "BlockWall", new Color(0.42f, 0.43f, 0.45f));
            AcousticNavy = Lit("M_AcousticNavy", "AcousticFabric", new Color(0.13f, 0.18f, 0.27f));
            AcousticGraphite = Lit("M_AcousticGraphite", "AcousticFabric", new Color(0.20f, 0.21f, 0.22f));
            RubberBerm = Lit("M_RubberBerm", "RubberBerm", Color.white);
            SteelBrushed = Lit("M_SteelBrushed", "SteelBrushed", Color.white);
            SteelDark = Lit("M_SteelDark", "PaintedMetal", new Color(0.16f, 0.17f, 0.18f));
            PaintedWhite = Lit("M_PaintedWhite", "PaintedMetal", new Color(0.92f, 0.92f, 0.90f));
            PaintedYellow = Lit("M_PaintedYellow", "PaintedMetal", new Color(0.95f, 0.72f, 0.06f));
            PaintedRed = Lit("M_PaintedRed", "PaintedMetal", new Color(0.72f, 0.06f, 0.05f));
            PaintedGrey = Lit("M_PaintedGrey", "PaintedMetal", new Color(0.45f, 0.47f, 0.49f));
            PaintedBlueGrey = Lit("M_PaintedBlueGrey", "PaintedMetal", new Color(0.30f, 0.38f, 0.46f));
            WoodCounter = Lit("M_WoodCounter", "WoodLaminate", Color.white);
            CeilingDeck = Lit("M_CeilingDeck", "CeilingDeck", new Color(0.55f, 0.56f, 0.58f));
            CeilingTile = Lit("M_CeilingTile", "CeilingTile", Color.white);
            Hazard = Lit("M_Hazard", "Hazard", Color.white);
            RubberMat = Lit("M_RubberMat", "RubberMat", Color.white);
            Pegboard = Lit("M_Pegboard", "Pegboard", Color.white);
            Cardboard = Lit("M_Cardboard", "Cardboard", Color.white);
            PlasticBlack = Lit("M_PlasticBlack", "PaintedMetal", new Color(0.05f, 0.05f, 0.055f));
            Fixture = Lit("M_LightFixture", "PaintedMetal", new Color(0.85f, 0.86f, 0.87f));

            FloorLineYellow = Lit("M_FloorLineYellow", "PaintedMetal", new Color(0.95f, 0.75f, 0.08f));
            FloorLineWhite = Lit("M_FloorLineWhite", "PaintedMetal", new Color(0.9f, 0.9f, 0.88f));
            FloorLineRed = Lit("M_FloorLineRed", "PaintedMetal", new Color(0.75f, 0.08f, 0.06f));

            Glass = Transparent("M_Glass", new Color(0.75f, 0.85f, 0.9f, 0.14f), 0.96f);
            ScreenGlass = Emissive("M_Screen", new Color(0.01f, 0.012f, 0.015f), new Color(0.02f, 0.05f, 0.08f), 0.9f);
            LedWhite = Emissive("M_LedWhite", Color.white, new Color(1f, 0.98f, 0.95f) * 4f, 0.5f);
            LedWarm = Emissive("M_LedWarm", Color.white, new Color(1f, 0.85f, 0.65f) * 3f, 0.5f);
            ExitGreen = Emissive("M_ExitGreen", new Color(0.05f, 0.4f, 0.12f), new Color(0.1f, 1f, 0.3f) * 2.5f, 0.6f);
            BeaconRed = Emissive("M_BeaconRed", new Color(0.4f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.03f) * 3f, 0.7f);
            BeaconGreen = Emissive("M_BeaconGreen", new Color(0.02f, 0.3f, 0.05f), new Color(0.05f, 1f, 0.15f) * 1.2f, 0.7f);
        }

        // ---------------------------------------------------------------- material helpers

        private void Tex(string name, int size, float worldSize, float normalStrength, Sampler sampler,
            string folder, bool force)
        {
            _textures[name] = GetOrCreate(folder, name, size, worldSize, normalStrength, sampler, force);
        }

        private Material LoadOrCreate(string name)
        {
            var path = $"{_materialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.shaderKeywords = new string[0];
            return mat;
        }

        private Material Lit(string name, string textureSet, Color tint)
        {
            var t = _textures[textureSet];
            var mat = LoadOrCreate(name);
            var tiling = Vector2.one / t.WorldSize;

            mat.SetTexture(BaseMap, t.Albedo);
            mat.SetTextureScale(BaseMap, tiling);
            mat.SetColor(BaseColor, tint);
            mat.SetTexture(BumpMap, t.Normal);
            mat.SetFloat(BumpScale, 1f);
            mat.SetTexture(MetallicGlossMap, t.Mask);
            mat.SetTexture(OcclusionMap, t.Mask);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.SetFloat(Metallic, 1f);
            mat.SetFloat(Smoothness, 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Material Transparent(string name, Color color, float smoothness)
        {
            var mat = LoadOrCreate(name);
            mat.SetColor(BaseColor, color);
            mat.SetFloat(Metallic, 0f);
            mat.SetFloat(Smoothness, smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_BlendModePreserveSpecular", 1f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Material Emissive(string name, Color baseColor, Color emission, float smoothness)
        {
            var mat = LoadOrCreate(name);
            mat.SetColor(BaseColor, baseColor);
            mat.SetFloat(Metallic, 0f);
            mat.SetFloat(Smoothness, smoothness);
            mat.SetColor(EmissionColor, emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- surfaces

        private static Color Grey(float v) => new Color(v, v, v);

        /// <summary>Distance (in tile units) to the nearest multiple of <paramref name="step"/>.</summary>
        private static float LineDistance(float t, float step) => Mathf.Abs(Frac(t / step + 0.5f) - 0.5f) * step;

        private static Surface ConcreteFloorSurface(float u, float v)
        {
            // 4 m tile, saw-cut joints every 2 m, polished warm-grey concrete with aggregate speckle.
            var broad = Fbm(u, v, 3, 5, 11);
            var fine = Fbm(u, v, 48, 3, 12);
            var stain = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Fbm(u, v, 2, 4, 13) - 0.55f) * 5f));
            var speck = Worley(u, v, 220, 14, out var id);
            var aggregate = speck < 0.22f ? (id - 0.5f) * 0.18f : 0f;

            var value = 0.60f + (broad - 0.5f) * 0.12f + (fine - 0.5f) * 0.05f + aggregate - stain * 0.07f;
            var joint = Mathf.Min(LineDistance(u, 0.5f), LineDistance(v, 0.5f)) * 4f; // metres
            var groove = Mathf.Clamp01(joint / 0.004f);
            value *= Mathf.Lerp(0.45f, 1f, groove);

            var albedo = new Color(value * 1.0f, value * 0.985f, value * 0.95f);
            var height = 0.5f + (fine - 0.5f) * 0.25f - (1f - groove) * 0.8f;
            var smooth = Mathf.Lerp(0.18f, 0.55f, groove) - stain * 0.15f + (fine - 0.5f) * 0.1f;
            return new Surface(albedo, height, smooth, 0f, Mathf.Lerp(0.55f, 1f, groove));
        }

        private static Surface ConcreteDownrangeSurface(float u, float v)
        {
            var broad = Fbm(u, v, 4, 5, 21);
            var fine = Fbm(u, v, 64, 3, 22);
            var scuff = Fbm(u, v, 6, 16, 4, 23);
            var stain = Mathf.Clamp01((Fbm(u, v, 3, 4, 24) - 0.5f) * 4f);
            var pit = Worley(u, v, 160, 25, out var id);
            var pitted = pit < 0.12f && id > 0.6f ? 1f : 0f;

            var value = 0.36f + (broad - 0.5f) * 0.14f + (fine - 0.5f) * 0.06f - stain * 0.08f + (scuff - 0.5f) * 0.05f;
            value -= pitted * 0.1f;
            var joint = Mathf.Min(LineDistance(u, 0.5f), LineDistance(v, 0.5f)) * 4f;
            var groove = Mathf.Clamp01(joint / 0.005f);
            value *= Mathf.Lerp(0.5f, 1f, groove);

            var albedo = new Color(value, value * 0.99f, value * 0.97f);
            var height = 0.5f + (fine - 0.5f) * 0.4f - pitted * 0.5f - (1f - groove) * 0.8f;
            var smooth = 0.22f + stain * 0.1f + (fine - 0.5f) * 0.1f;
            return new Surface(albedo, height, smooth, 0f, Mathf.Lerp(0.6f, 1f, groove));
        }

        private static Surface BlockWallSurface(float u, float v)
        {
            // 2 m tile of 400 x 200 mm painted concrete blocks in running bond.
            const float cols = 5f, rows = 10f;
            var row = Mathf.FloorToInt(v * rows);
            var bx = u * cols + (row % 2) * 0.5f;
            var col = Mathf.FloorToInt(bx);
            var fx = bx - col;
            var fy = v * rows - row;

            var mx = Mathf.Min(fx, 1f - fx) * 0.4f; // metres to the vertical joint
            var my = Mathf.Min(fy, 1f - fy) * 0.2f; // metres to the horizontal joint
            var edge = Mathf.Min(mx, my);
            var face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.004f) / 0.008f));

            var blockTint = (Value(Mod1(col / cols), row / rows, 64, 31) - 0.5f) * 0.04f;
            var pores = Worley(u, v, 180, 32, out var pid);
            var pore = pores < 0.18f && pid > 0.5f ? 1f : 0f;
            var paint = Fbm(u, v, 32, 3, 33);

            var value = 0.86f + blockTint + (paint - 0.5f) * 0.05f - pore * 0.12f;
            value = Mathf.Lerp(value * 0.82f, value, face);
            var height = face * 0.8f + (paint - 0.5f) * 0.15f - pore * 0.3f;
            return new Surface(Grey(value), height, 0.22f + face * 0.06f, 0f, Mathf.Lerp(0.6f, 1f, face));
        }

        private static float Mod1(float x) => x - Mathf.Floor(x);

        private static Surface AcousticSurface(float u, float v)
        {
            const float threads = 220f;
            var wx = Mathf.Sin(u * threads * Mathf.PI * 2f);
            var wy = Mathf.Sin(v * threads * Mathf.PI * 2f);
            var weave = 0.5f + 0.25f * (wx + wy);
            var fuzz = Fbm(u, v, 64, 3, 41);
            var value = 0.82f + (weave - 0.5f) * 0.12f + (fuzz - 0.5f) * 0.1f;
            return new Surface(Grey(value), weave * 0.6f + fuzz * 0.4f, 0.04f, 0f, 0.9f + weave * 0.1f);
        }

        private static Surface RubberBermSurface(float u, float v)
        {
            var d = Worley(u, v, 110, 51, out var id);
            var granule = Mathf.Clamp01(1f - d * 1.6f);
            var value = 0.06f + id * 0.05f + granule * 0.03f;
            var dust = Mathf.Clamp01((Fbm(u, v, 4, 4, 52) - 0.55f) * 3f) * 0.08f;
            return new Surface(new Color(value + dust, value + dust * 0.9f, value + dust * 0.8f), granule,
                0.12f + granule * 0.15f, 0f, 0.6f + granule * 0.4f);
        }

        private static Surface SteelBrushedSurface(float u, float v)
        {
            var streak = Fbm(u, v, 2, 160, 4, 61);
            var blotch = Fbm(u, v, 3, 4, 62);
            var value = 0.58f + (streak - 0.5f) * 0.14f + (blotch - 0.5f) * 0.06f;
            return new Surface(Grey(value), streak, 0.55f + (streak - 0.5f) * 0.2f, 1f, 1f);
        }

        private static Surface PaintedMetalSurface(float u, float v)
        {
            // Powder coat: very light orange-peel and a hint of handling wear.
            var peel = Fbm(u, v, 48, 3, 71);
            var wear = Mathf.Clamp01((Fbm(u, v, 4, 5, 72) - 0.62f) * 4f);
            var value = 0.97f + (peel - 0.5f) * 0.04f - wear * 0.08f;
            return new Surface(Grey(value), peel * 0.5f, 0.42f + (peel - 0.5f) * 0.1f - wear * 0.1f, wear * 0.4f, 1f);
        }

        private static Surface WoodSurface(float u, float v)
        {
            // Lacquered beech-look laminate with the grain running along U.
            var warp = Fbm(u, v, 2, 6, 4, 81);
            var rings = Frac(v * 14f + warp * 2.5f);
            var ring = Mathf.SmoothStep(0f, 1f, Mathf.Abs(rings - 0.5f) * 2f);
            var fibre = Value(u, v, 6, 400, 82);
            var light = new Color(0.60f, 0.47f, 0.34f);
            var dark = new Color(0.50f, 0.37f, 0.25f);
            var albedo = Color.Lerp(dark, light, ring * 0.45f + fibre * 0.55f);
            return new Surface(albedo, fibre * 0.4f + ring * 0.2f, 0.55f + fibre * 0.05f, 0f, 1f);
        }

        private static Surface CeilingDeckSurface(float u, float v)
        {
            // Galvanised trapezoidal steel deck, ribs every 300 mm along U.
            var p = Frac(u * 4f);
            var rib = Mathf.Clamp01(Mathf.Min(Mathf.Abs(p - 0.25f), Mathf.Abs(p - 0.75f)) < 0.12f ? 1f : 0f);
            var slope = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Abs(p - 0.5f) - 0.25f) / 0.13f);
            var height = Mathf.Lerp(0f, 1f, Mathf.SmoothStep(0f, 1f, slope));
            var spangle = Worley(u, v, 40, 91, out var id);
            var value = 0.62f + (id - 0.5f) * 0.08f + (Fbm(u, v, 6, 3, 92) - 0.5f) * 0.08f;
            return new Surface(Grey(value), height, 0.4f + (id - 0.5f) * 0.15f + spangle * 0.02f, 0.65f,
                Mathf.Lerp(0.7f, 1f, height) * (rib > 0 ? 1f : 0.95f));
        }

        private static Surface CeilingTileSurface(float u, float v)
        {
            // 600 mm mineral-fibre tiles in a white T-bar grid (2 x 2 tiles per texture).
            var gx = LineDistance(u, 0.5f) * 1.2f;
            var gy = LineDistance(v, 0.5f) * 1.2f;
            var grid = Mathf.Min(gx, gy);
            var isBar = grid < 0.012f;
            var bevel = Mathf.Clamp01((grid - 0.012f) / 0.01f);

            var fissure = Worley(u, v, 90, 101, out var id);
            var hole = fissure < 0.16f && id > 0.55f ? 1f : 0f;
            var fibre = Fbm(u, v, 64, 3, 102);
            if (isBar) return new Surface(Grey(0.93f), 1f, 0.6f, 0.2f, 1f);
            var value = 0.9f + (fibre - 0.5f) * 0.05f - hole * 0.25f;
            return new Surface(Grey(value), 0.6f * bevel + fibre * 0.15f - hole * 0.4f, 0.08f, 0f,
                Mathf.Lerp(0.65f, 1f, bevel));
        }

        private static Surface HazardSurface(float u, float v)
        {
            var stripe = Frac((u + v) * 3f) < 0.5f;
            var wear = Mathf.Clamp01((Fbm(u, v, 8, 5, 111) - 0.6f) * 4f);
            var c = stripe ? new Color(0.95f, 0.72f, 0.05f) : new Color(0.05f, 0.05f, 0.05f);
            c = Color.Lerp(c, Grey(0.45f), wear * 0.6f);
            return new Surface(c, 0.5f - wear * 0.2f, 0.4f - wear * 0.2f, 0f, 1f);
        }

        private static Surface RubberMatSurface(float u, float v)
        {
            // Anti-fatigue mat with raised round studs.
            var su = Frac(u * 12f) - 0.5f;
            var sv = Frac(v * 12f) - 0.5f;
            var r = Mathf.Sqrt(su * su + sv * sv);
            var stud = Mathf.Clamp01((0.32f - r) / 0.06f);
            var grit = Fbm(u, v, 128, 2, 121);
            var value = 0.07f + stud * 0.02f + (grit - 0.5f) * 0.02f;
            return new Surface(Grey(value), stud, 0.25f + stud * 0.1f, 0f, Mathf.Lerp(0.8f, 1f, stud));
        }

        private static Surface PegboardSurface(float u, float v)
        {
            // Hardboard with 6 mm holes on a 25 mm grid (0.5 m tile).
            var dx = LineDistance(u, 1f / 20f) * 0.5f;
            var dy = LineDistance(v, 1f / 20f) * 0.5f;
            var r = Mathf.Sqrt(dx * dx + dy * dy);
            var hole = Mathf.Clamp01((0.0035f - r) / 0.001f);
            var fibre = Fbm(u, v, 64, 3, 131);
            var board = new Color(0.62f, 0.5f, 0.36f) * (0.95f + (fibre - 0.5f) * 0.1f);
            var albedo = Color.Lerp(board, Grey(0.04f), hole);
            return new Surface(albedo, 1f - hole, 0.25f, 0f, 1f - hole * 0.7f);
        }

        private static Surface CardboardSurface(float u, float v)
        {
            var flute = 0.5f + 0.5f * Mathf.Sin(u * 120f * Mathf.PI);
            var fibre = Fbm(u, v, 32, 4, 141);
            var c = new Color(0.62f, 0.47f, 0.3f) * (0.92f + (fibre - 0.5f) * 0.16f);
            return new Surface(c, flute * 0.2f + fibre * 0.3f, 0.12f, 0f, 1f);
        }
    }
}

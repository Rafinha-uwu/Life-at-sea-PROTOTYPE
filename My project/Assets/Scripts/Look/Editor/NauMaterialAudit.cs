using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Lists every material the open scene actually renders with, flags the ones
// that clash with a unified look, and can clamp the outliers. Audit first,
// normalize only after reading the report.
public static class NauMaterialAudit
{
    private const string ReportPath = "MaterialAudit.md";

    // Above this a non-metal reads as plastic under the warm grade
    private const float MaxSmoothness = 0.45f;
    private const float MaxSaturation = 0.75f;
    private const float MaxValue = 0.9f;

    // Materials that are allowed to be shiny
    private static readonly string[] ShinyHints =
    {
        "glass", "metal", "gold", "brass", "bronze", "copper", "iron", "steel",
        "water", "sea", "ocean", "lantern", "candle", "flame", "lens", "mirror",
        "bottle", "jar", "bell", "coin", "blade", "sword", "cannon",
    };

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int MetallicMap = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private class Entry
    {
        public Material Material;
        public string Shader;
        public float Smoothness = -1f;
        public float Metallic = -1f;
        public bool HasMetallicMap;
        public bool HasBaseMap;
        public Color Base = Color.white;
        public bool Emissive;
        public float EmissionLuminance;
        public int Renderers;
        public List<string> Flags = new();
    }

    [MenuItem("Nau/Look/3. Audit Scene Materials")]
    private static void Audit()
    {
        List<Entry> entries = Collect();
        var sb = new StringBuilder();

        sb.AppendLine("# Material audit");
        sb.AppendLine();
        sb.AppendLine($"{entries.Count} materials used by renderers in the open scene(s).");
        sb.AppendLine();

        sb.AppendLine("## Shaders");
        sb.AppendLine();
        sb.AppendLine("| Count | Shader |");
        sb.AppendLine("|---:|---|");

        foreach (var g in entries.GroupBy(e => e.Shader).OrderByDescending(g => g.Count()))
            sb.AppendLine($"| {g.Count()} | {g.Key} |");

        sb.AppendLine();
        sb.AppendLine("## Flagged");
        sb.AppendLine();
        sb.AppendLine("| Material | Shader | Smooth | Metal | Base (HSV) | Emissive | Renderers | Flags | Path |");
        sb.AppendLine("|---|---|---:|---:|---|---|---:|---|---|");

        int flagged = 0;

        foreach (Entry e in entries.Where(e => e.Flags.Count > 0)
                     .OrderByDescending(e => e.Flags.Count).ThenByDescending(e => e.Renderers))
        {
            flagged++;
            sb.AppendLine(Row(e));
        }

        sb.AppendLine();
        sb.AppendLine("## Everything");
        sb.AppendLine();
        sb.AppendLine("| Material | Shader | Smooth | Metal | Base (HSV) | Emissive | Renderers | Flags | Path |");
        sb.AppendLine("|---|---|---:|---:|---|---|---:|---|---|");

        foreach (Entry e in entries.OrderBy(e => e.Shader).ThenBy(e => e.Material.name))
            sb.AppendLine(Row(e));

        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"Audited {entries.Count} materials, {flagged} flagged. Report: {Path.GetFullPath(ReportPath)}");
    }

    [MenuItem("Nau/Look/4. Normalize Flagged Materials")]
    private static void Normalize()
    {
        List<Entry> entries = Collect();
        int changed = 0;

        foreach (Entry e in entries)
        {
            if (e.Flags.Count == 0) continue;

            Material m = e.Material;
            bool touched = false;
            Undo.RecordObject(m, "Normalize Material");

            if (e.Flags.Contains("smooth") && m.HasProperty(Smoothness))
            {
                // Flat colours with no texture read as plastic sooner than textured ones
                m.SetFloat(Smoothness, e.HasBaseMap ? MaxSmoothness : 0.3f);
                touched = true;
            }

            if (e.Flags.Contains("metallic") && m.HasProperty(Metallic))
            {
                m.SetFloat(Metallic, 0f);
                touched = true;
            }

            if ((e.Flags.Contains("saturated") || e.Flags.Contains("bright")) && m.HasProperty(BaseColor))
            {
                Color.RGBToHSV(e.Base, out float h, out float s, out float v);
                Color c = Color.HSVToRGB(h, Mathf.Min(s, MaxSaturation), Mathf.Min(v, MaxValue));
                c.a = e.Base.a;
                m.SetColor(BaseColor, c);
                touched = true;
            }

            if (!touched) continue;

            EditorUtility.SetDirty(m);
            changed++;
            Debug.Log($"Normalized {m.name} ({string.Join(", ", e.Flags)})", m);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Normalized {changed} materials. Undo with Ctrl+Z if it went too far.");
    }

    private static List<Entry> Collect()
    {
        var byMaterial = new Dictionary<Material, Entry>();

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !AssetDatabase.Contains(m)) continue;

                if (!byMaterial.TryGetValue(m, out Entry e))
                {
                    e = Describe(m);
                    byMaterial.Add(m, e);
                }

                e.Renderers++;
            }
        }

        return byMaterial.Values.ToList();
    }

    private static Entry Describe(Material m)
    {
        var e = new Entry { Material = m, Shader = m.shader != null ? m.shader.name : "(missing)" };

        if (m.HasProperty(Smoothness)) e.Smoothness = m.GetFloat(Smoothness);
        if (m.HasProperty(Metallic)) e.Metallic = m.GetFloat(Metallic);
        if (m.HasProperty(MetallicMap)) e.HasMetallicMap = m.GetTexture(MetallicMap) != null;
        if (m.HasProperty(BaseMap)) e.HasBaseMap = m.GetTexture(BaseMap) != null;
        if (m.HasProperty(BaseColor)) e.Base = m.GetColor(BaseColor);

        e.Emissive = m.IsKeywordEnabled("_EMISSION") && m.HasProperty(EmissionColor);
        if (e.Emissive)
        {
            Color c = m.GetColor(EmissionColor);
            e.EmissionLuminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        bool isUrpLit = e.Shader.StartsWith("Universal Render Pipeline/") &&
                        !e.Shader.Contains("Particles") && !e.Shader.Contains("Unlit");
        if (!isUrpLit) return e;

        bool allowedShiny = ShinyHints.Any(h => m.name.ToLowerInvariant().Contains(h));

        if (e.Smoothness > MaxSmoothness && !e.HasMetallicMap && !allowedShiny)
            e.Flags.Add("smooth");

        if (e.Metallic > 0.5f && !e.HasMetallicMap && !allowedShiny)
            e.Flags.Add("metallic");

        // A white tint over a texture is normal. A strong tint over a texture is
        // usually someone forcing a colour, so it gets clamped. A flat saturated
        // colour with no texture is probably a gameplay signal, so it is only listed.
        Color.RGBToHSV(e.Base, out _, out float s, out float v);
        if (s > MaxSaturation && v > 0.4f) e.Flags.Add(e.HasBaseMap ? "saturated" : "flat-saturated");
        if (v > MaxValue && s < 0.15f && !e.HasBaseMap) e.Flags.Add("bright");

        if (e.Emissive && e.EmissionLuminance > 2f) e.Flags.Add("hot-emissive");

        return e;
    }

    private static string Row(Entry e)
    {
        Color.RGBToHSV(e.Base, out float h, out float s, out float v);
        string smooth = e.Smoothness < 0 ? "-" : e.Smoothness.ToString("0.00");
        string metal = e.Metallic < 0 ? "-" : e.Metallic.ToString("0.00") + (e.HasMetallicMap ? " (map)" : "");
        string baseMap = e.HasBaseMap ? "tex " : "flat ";
        string emis = e.Emissive ? e.EmissionLuminance.ToString("0.0") : "-";
        string path = AssetDatabase.GetAssetPath(e.Material);

        return $"| {e.Material.name} | {e.Shader.Replace("Universal Render Pipeline/", "URP/")} | {smooth} | {metal} | " +
               $"{baseMap}{h * 360f:0} {s:0.00} {v:0.00} | {emis} | {e.Renderers} | {string.Join(" ", e.Flags)} | {path} |";
    }
}

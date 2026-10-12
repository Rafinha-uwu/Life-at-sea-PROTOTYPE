using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// Moshin-Regular has no accented letters and no accent marks at all, so the
// TMP atlas cannot simply be regenerated with them. This builds them straight
// into the TMP font asset instead: each new letter is the existing base letter
// with an accent laid over it, drawn into free space in the atlas.
//
//   acute / grave  - the font's own apostrophe, tilted
//   cedilla        - the font's own comma, tucked under the c
//   circumflex / tilde - drawn strokes, as thick as the apostrophe
//   i              - the dot is cut off before the accent goes on
//
// Everything is done in SDF space, so the new letters render exactly like the
// rest - outlines, softness and all. Safe to run again: it removes the letters
// it made last time and rebuilds them with the current settings.
public class FontAccentsWizard : ScriptableWizard
{
    [Tooltip("The TMP font assets to add the letters to.")]
    public TMP_FontAsset[] fonts;

    [Header("Placement")]
    [Tooltip("Space between the letter and the accent, in atlas pixels.")]
    public float gap = 4f;

    [Tooltip("Accents on capitals are made this much smaller, so they do not crash into the line above.")]
    [Range(0.5f, 1f)] public float uppercaseScale = 0.85f;

    [Header("Acute and grave (the font's apostrophe)")]
    [Range(0.4f, 1.5f)] public float acuteScale = 0.9f;
    [Range(0f, 80f)] public float acuteAngle = 40f;

    [Header("Circumflex and tilde (drawn)")]
    [Tooltip("Stroke thickness in atlas pixels. 0 = match the apostrophe.")]
    public float strokeThickness = 0f;

    [Tooltip("Width and height of the ^.")]
    public Vector2 circumflexSize = new(17f, 9f);

    [Tooltip("Width and height of the ~.")]
    public Vector2 tildeSize = new(19f, 6f);

    [Header("Cedilla (the font's comma)")]
    [Range(0.4f, 1.5f)] public float cedillaScale = 0.9f;

    [Tooltip("How far the tail tucks up into the bottom of the c.")]
    public float cedillaOverlap = 2f;

    [Header("Safety")]
    [Tooltip("Copies each font asset to FontBackups/ (next to Assets) first. Copy it back over the original to undo.")]
    public bool backupFirst = true;

    private enum Mark { Acute, Grave, Circumflex, Tilde, Cedilla }

    private static readonly (char letter, char baseLetter, Mark mark)[] Letters =
    {
        ('á', 'a', Mark.Acute), ('à', 'a', Mark.Grave), ('â', 'a', Mark.Circumflex), ('ã', 'a', Mark.Tilde),
        ('é', 'e', Mark.Acute), ('ê', 'e', Mark.Circumflex),
        ('í', 'i', Mark.Acute),
        ('ó', 'o', Mark.Acute), ('ô', 'o', Mark.Circumflex), ('õ', 'o', Mark.Tilde),
        ('ú', 'u', Mark.Acute),
        ('ç', 'c', Mark.Cedilla),
        ('Á', 'A', Mark.Acute), ('À', 'A', Mark.Grave), ('Â', 'A', Mark.Circumflex), ('Ã', 'A', Mark.Tilde),
        ('É', 'E', Mark.Acute), ('Ê', 'E', Mark.Circumflex),
        ('Í', 'I', Mark.Acute),
        ('Ó', 'O', Mark.Acute), ('Ô', 'O', Mark.Circumflex), ('Õ', 'O', Mark.Tilde),
        ('Ú', 'U', Mark.Acute),
        ('Ç', 'C', Mark.Cedilla),
    };

    // Our glyphs get indices well clear of the font's own
    private const uint GlyphIndexBase = 900000;

    [MenuItem("Nau/Fonts/Add Portuguese Accents")]
    private static void Open()
    {
        FontAccentsWizard wizard = DisplayWizard<FontAccentsWizard>("Add Portuguese Accents", "Add Letters");

        List<TMP_FontAsset> found = new();

        foreach (Object selected in Selection.objects)
            if (selected is TMP_FontAsset font) found.Add(font);

        if (found.Count == 0)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset Moshin"))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null) found.Add(font);
            }
        }

        wizard.fonts = found.ToArray();
        wizard.OnWizardUpdate();
    }

    private void OnWizardUpdate()
    {
        isValid = fonts != null && fonts.Length > 0;
        helpString = isValid
            ? $"Adds {Letters.Length} letters (a e i o u with accents, c cedilla, upper and lower case) to {fonts.Length} font asset(s). Run it again to tweak - it replaces what it made last time."
            : "Drag the TMP font asset(s) into Fonts.";
    }

    private void OnWizardCreate()
    {
        foreach (TMP_FontAsset font in fonts)
        {
            if (font == null) continue;

            try
            {
                Build(font);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Adding accents to {font.name} failed: {e}", font);
            }
        }
    }

    // ---------------------------------------------------------------------

    private TMP_FontAsset font;
    private float[] atlas;
    private int atlasW, atlasH, pad;
    private float gradient;   // pixels from the edge to full black/white in the SDF

    private void Build(TMP_FontAsset target)
    {
        font = target;
        string path = AssetDatabase.GetAssetPath(font);

        if (font.atlasPopulationMode != AtlasPopulationMode.Static)
            Debug.LogWarning($"{font.name} is Dynamic - set it to Static, or TMP may repack the atlas over the new letters.", font);

        if (backupFirst)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "FontBackups");
            Directory.CreateDirectory(folder);
            string backup = Path.Combine(folder, $"{font.name} {System.DateTime.Now:yyyy-MM-dd HHmmss}.asset");
            AssetDatabase.SaveAssets();
            File.Copy(Path.GetFullPath(path), backup, true);
            Debug.Log($"Backed up {font.name} to {backup}");
        }

        pad = font.atlasPadding;
        gradient = pad + 1f;   // what TMP puts in the material's Gradient Scale

        // Out with last run's letters, so this can be re-run freely
        HashSet<uint> ours = new();
        foreach (var entry in Letters) ours.Add(entry.letter);

        font.characterTable.RemoveAll(c => ours.Contains(c.unicode));
        font.glyphTable.RemoveAll(g => g.index >= GlyphIndexBase);
        font.ReadFontAssetDefinition();

        Texture2D texture = font.atlasTexture;
        atlasW = texture.width;
        atlasH = texture.height;
        atlas = ReadAlpha(texture);

        Glyph apostrophe = Find('\'') ?? Find('’');
        Glyph comma = Find(',');
        Glyph n = Find('n');

        float thickness = strokeThickness > 0f ? strokeThickness : MeasureStroke(apostrophe);
        float xHeight = n != null ? n.metrics.horizontalBearingY : font.faceInfo.meanLine;

        // Render every new letter first, then pack them
        List<(char letter, float bx, float by, int w, int h, float advance, float[] pixels)> made = new();

        foreach (var (letter, baseLetter, mark) in Letters)
        {
            Glyph baseGlyph = Find(baseLetter);

            if (baseGlyph == null)
            {
                Debug.LogWarning($"{font.name} has no '{baseLetter}', so '{letter}' was skipped.", font);
                continue;
            }

            bool upper = char.IsUpper(letter);
            float size = upper ? uppercaseScale : 1f;

            // The i loses its dot: everything above the x-height is cut away
            bool dotless = baseLetter == 'i';
            float cut = xHeight + 0.5f;

            GlyphMetrics bm = baseGlyph.metrics;
            float baseLeft = bm.horizontalBearingX;
            float baseRight = bm.horizontalBearingX + bm.width;
            float baseTop = dotless ? Mathf.Min(bm.horizontalBearingY, cut) : bm.horizontalBearingY;
            float baseBottom = bm.horizontalBearingY - bm.height;
            float centreX = (baseLeft + baseRight) * 0.5f;

            System.Func<Vector2, float> accent;
            Rect accentBox;

            switch (mark)
            {
                case Mark.Acute:
                case Mark.Grave:
                {
                    if (apostrophe == null) { Debug.LogWarning($"{font.name} has no apostrophe for the acute/grave."); continue; }
                    float angle = (mark == Mark.Acute ? -acuteAngle : acuteAngle);
                    (accent, accentBox) = FromGlyph(apostrophe, acuteScale * size, angle, centreX, baseTop + gap * size, true);
                    break;
                }

                case Mark.Cedilla:
                {
                    if (comma == null) { Debug.LogWarning($"{font.name} has no comma for the cedilla."); continue; }
                    (accent, accentBox) = FromGlyph(comma, cedillaScale * size, 0f, centreX, baseBottom + cedillaOverlap, false);
                    break;
                }

                case Mark.Circumflex:
                {
                    float w = circumflexSize.x * size, h = circumflexSize.y * size;
                    Vector2[] line = { new(-w / 2f, 0f), new(0f, h), new(w / 2f, 0f) };
                    (accent, accentBox) = Stroke(line, thickness * Mathf.Lerp(1f, size, 0.5f), centreX, baseTop + gap * size);
                    break;
                }

                default: // Tilde
                {
                    float w = tildeSize.x * size, h = tildeSize.y * size;
                    List<Vector2> line = new();
                    const int steps = 24;

                    // Up on the left, down on the right, ends a little short of a full wave
                    for (int s = 0; s <= steps; s++)
                    {
                        float u = Mathf.Lerp(-0.42f, 0.42f, s / (float)steps);
                        line.Add(new Vector2(u * w, -Mathf.Sin(u * Mathf.PI * 2f) * h * 0.5f));
                    }

                    (accent, accentBox) = Stroke(line.ToArray(), thickness * Mathf.Lerp(1f, size, 0.5f), centreX, baseTop + gap * size);
                    break;
                }
            }

            // The new letter's box: the base letter plus its accent
            float left = Mathf.Min(baseLeft, accentBox.xMin);
            float right = Mathf.Max(baseRight, accentBox.xMax);
            float bottom = Mathf.Min(baseBottom, accentBox.yMin);
            float top = Mathf.Max(baseTop, accentBox.yMax);

            int pw = Mathf.CeilToInt(right - left);
            int ph = Mathf.CeilToInt(top - bottom);
            float bx = left, by = bottom + ph;

            int fullW = pw + pad * 2, fullH = ph + pad * 2;
            float[] pixels = new float[fullW * fullH];

            for (int j = 0; j < fullH; j++)
            {
                for (int i = 0; i < fullW; i++)
                {
                    // Glyph space at this pixel's centre (x from the pen, y from the baseline)
                    Vector2 p = new(bx + (i - pad) + 0.5f, (by - ph) + (j - pad) + 0.5f);

                    float v = SampleGlyph(baseGlyph, p);

                    // Intersect with "below the cut" to drop the dot
                    if (dotless) v = Mathf.Min(v, Mathf.Clamp01(0.5f + (cut - p.y) / (2f * gradient)));

                    // Union of the two shapes is the larger SDF value
                    v = Mathf.Max(v, accent(p));

                    pixels[j * fullW + i] = v;
                }
            }

            made.Add((letter, bx, by, pw, ph, bm.horizontalAdvance, pixels));
        }

        Pack(made);
        WriteAtlas(texture);

        font.ReadFontAssetDefinition();
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);

        Debug.Log($"{font.name}: added {made.Count} accented letters. Atlas is {atlasW}x{atlasH}.", font);
    }

    // ---------------------------------------------------------------------
    // Shapes

    // An accent made from one of the font's own glyphs, scaled and tilted.
    // Placed with its bottom at y (on top of the letter), or its top at y
    // for a mark that hangs below.
    private (System.Func<Vector2, float>, Rect) FromGlyph(Glyph source, float scale, float degrees, float centreX, float y, bool above)
    {
        GlyphMetrics m = source.metrics;
        Vector2 sourceCentre = new(m.horizontalBearingX + m.width / 2f, m.horizontalBearingY - m.height / 2f);

        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);

        Vector2 Forward(Vector2 q)
        {
            Vector2 d = (q - sourceCentre) * scale;
            return new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
        }

        // Where the source's box ends up, before moving it into place
        Vector2[] corners =
        {
            Forward(new Vector2(m.horizontalBearingX, m.horizontalBearingY - m.height)),
            Forward(new Vector2(m.horizontalBearingX + m.width, m.horizontalBearingY - m.height)),
            Forward(new Vector2(m.horizontalBearingX, m.horizontalBearingY)),
            Forward(new Vector2(m.horizontalBearingX + m.width, m.horizontalBearingY)),
        };

        Vector2 min = corners[0], max = corners[0];
        foreach (Vector2 c in corners) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }

        Vector2 offset = new(centreX - (min.x + max.x) / 2f, above ? y - min.y : y - max.y);
        Rect box = Rect.MinMaxRect(min.x + offset.x, min.y + offset.y, max.x + offset.x, max.y + offset.y);

        float Sample(Vector2 p)
        {
            // Back into the source glyph's own space
            Vector2 d = (p - offset) / scale;
            Vector2 q = new Vector2(d.x * cos + d.y * sin, -d.x * sin + d.y * cos) + sourceCentre;

            // Shrinking a shape shrinks its distances too
            float v = SampleGlyph(source, q);
            return Mathf.Clamp01(0.5f + (v - 0.5f) * scale);
        }

        return (new System.Func<Vector2, float>(Sample), box);
    }

    // A drawn line with round ends, its bottom resting at y
    private (System.Func<Vector2, float>, Rect) Stroke(Vector2[] line, float thickness, float centreX, float y)
    {
        float half = thickness / 2f;

        Vector2 min = line[0], max = line[0];
        foreach (Vector2 point in line) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }

        Vector2 offset = new(centreX - (min.x + max.x) / 2f, y + half - min.y);
        Rect box = Rect.MinMaxRect(min.x - half + offset.x, min.y - half + offset.y, max.x + half + offset.x, max.y + half + offset.y);

        float Sample(Vector2 p)
        {
            Vector2 local = p - offset;
            float nearest = float.MaxValue;

            for (int k = 0; k < line.Length - 1; k++)
                nearest = Mathf.Min(nearest, DistanceToSegment(local, line[k], line[k + 1]));

            float distance = nearest - half;   // negative inside the stroke
            return Mathf.Clamp01(0.5f - distance / (2f * gradient));
        }

        return (new System.Func<Vector2, float>(Sample), box);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    // Width of the apostrophe's stroke, halfway down - so drawn accents match
    private float MeasureStroke(Glyph glyph)
    {
        if (glyph == null) return 5f;

        GlyphMetrics m = glyph.metrics;
        float y = m.horizontalBearingY - m.height / 2f;
        float inside = 0f;
        const float step = 0.25f;

        for (float x = m.horizontalBearingX - pad; x < m.horizontalBearingX + m.width + pad; x += step)
            if (SampleGlyph(glyph, new Vector2(x, y)) > 0.5f) inside += step;

        return inside > 1f ? inside : 5f;
    }

    // ---------------------------------------------------------------------
    // Atlas

    private Glyph Find(char c) =>
        font.characterLookupTable != null && font.characterLookupTable.TryGetValue(c, out TMP_Character character)
            ? character.glyph
            : null;

    // A glyph's SDF at a point in glyph space. TMP stretches the padded rect
    // over the padded metrics box, so this does the same
    private float SampleGlyph(Glyph glyph, Vector2 p)
    {
        GlyphMetrics m = glyph.metrics;
        GlyphRect r = glyph.glyphRect;

        float qw = m.width + pad * 2f, qh = m.height + pad * 2f;
        if (qw <= 0f || qh <= 0f) return 0f;

        float u = (p.x - (m.horizontalBearingX - pad)) / qw;
        float v = (p.y - (m.horizontalBearingY - m.height - pad)) / qh;

        if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;

        float ax = (r.x - pad) + u * (r.width + pad * 2f);
        float ay = (r.y - pad) + v * (r.height + pad * 2f);

        return Bilinear(ax, ay);
    }

    private float Bilinear(float x, float y)
    {
        x -= 0.5f; y -= 0.5f;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;

        float a = Pixel(x0, y0), b = Pixel(x0 + 1, y0);
        float c = Pixel(x0, y0 + 1), d = Pixel(x0 + 1, y0 + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private float Pixel(int x, int y)
    {
        x = Mathf.Clamp(x, 0, atlasW - 1);
        y = Mathf.Clamp(y, 0, atlasH - 1);
        return atlas[y * atlasW + x];
    }

    // Finds room for each new letter between the existing ones. If the atlas
    // is full it doubles in height - the old letters keep their pixel spots.
    private void Pack(List<(char letter, float bx, float by, int w, int h, float advance, float[] pixels)> made)
    {
        bool[] used = new bool[atlasW * atlasH];

        foreach (Glyph glyph in font.glyphTable)
        {
            GlyphRect r = glyph.glyphRect;
            if (r.width == 0 || r.height == 0) continue;
            Occupy(used, atlasW, r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2);
        }

        int[] sums = Integral(used, atlasW, atlasH);

        for (int n = 0; n < made.Count; n++)
        {
            var item = made[n];
            int fullW = item.w + pad * 2, fullH = item.h + pad * 2;

            if (!FindSpace(sums, fullW, fullH, out int sx, out int sy))
            {
                // Grow: copy the old atlas into the bottom of a taller one
                int newH = atlasH * 2;
                float[] grown = new float[atlasW * newH];
                System.Array.Copy(atlas, grown, atlas.Length);
                bool[] grownUsed = new bool[atlasW * newH];
                System.Array.Copy(used, grownUsed, used.Length);

                atlas = grown; used = grownUsed; atlasH = newH;
                sums = Integral(used, atlasW, atlasH);

                if (!FindSpace(sums, fullW, fullH, out sx, out sy))
                    throw new System.Exception($"No room for '{item.letter}' even after growing the atlas.");
            }

            // Write the letter, padding included
            for (int j = 0; j < fullH; j++)
                for (int i = 0; i < fullW; i++)
                    atlas[(sy + j) * atlasW + (sx + i)] = item.pixels[j * fullW + i];

            Occupy(used, atlasW, sx, sy, fullW, fullH);
            sums = Integral(used, atlasW, atlasH);

            uint index = GlyphIndexBase + item.letter;
            Glyph glyph = new(index,
                new GlyphMetrics(item.w, item.h, item.bx, item.by, item.advance),
                new GlyphRect(sx + pad, sy + pad, item.w, item.h),
                1f, 0);

            font.glyphTable.Add(glyph);
            font.characterTable.Add(new TMP_Character(item.letter, font, glyph));
        }
    }

    private static void Occupy(bool[] used, int width, int x, int y, int w, int h)
    {
        int height = used.Length / width;
        for (int j = Mathf.Max(0, y); j < Mathf.Min(height, y + h); j++)
            for (int i = Mathf.Max(0, x); i < Mathf.Min(width, x + w); i++)
                used[j * width + i] = true;
    }

    private static int[] Integral(bool[] used, int w, int h)
    {
        int[] sums = new int[(w + 1) * (h + 1)];

        for (int y = 0; y < h; y++)
        {
            int row = 0;
            for (int x = 0; x < w; x++)
            {
                row += used[y * w + x] ? 1 : 0;
                sums[(y + 1) * (w + 1) + (x + 1)] = sums[y * (w + 1) + (x + 1)] + row;
            }
        }

        return sums;
    }

    private bool FindSpace(int[] sums, int w, int h, out int sx, out int sy)
    {
        int stride = atlasW + 1;

        for (int y = 0; y + h <= atlasH; y++)
        {
            for (int x = 0; x + w <= atlasW; x++)
            {
                int filled = sums[(y + h) * stride + (x + w)] - sums[y * stride + (x + w)]
                           - sums[(y + h) * stride + x] + sums[y * stride + x];

                if (filled == 0) { sx = x; sy = y; return true; }
            }
        }

        sx = sy = 0;
        return false;
    }

    // Works whether or not the atlas is readable: a GPU copy is read back
    private static float[] ReadAlpha(Texture2D texture)
    {
        float[] values = new float[texture.width * texture.height];

        if (texture.isReadable)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) values[i] = pixels[i].a / 255f;
            return values;
        }

        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;

        Texture2D copy = new(texture.width, texture.height, TextureFormat.RGBA32, false, true);
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] read = copy.GetPixels32();
        for (int i = 0; i < read.Length; i++) values[i] = read[i].a / 255f;

        Object.DestroyImmediate(copy);
        return values;
    }

    private bool WroteCorrectly(Texture2D texture)
    {
        if (texture.width != atlasW || texture.height != atlasH) return false;

        float[] back = ReadAlpha(texture);
        for (int i = 0; i < back.Length; i += 97)
            if (Mathf.Abs(back[i] - Mathf.Clamp01(atlas[i])) > 0.02f) return false;

        return true;
    }

    // Puts the pixels into the font's existing atlas texture, keeping it the
    // same object so every material and text that points at it still does
    private void WriteAtlas(Texture2D texture)
    {
        bool resized = texture.width != atlasW || texture.height != atlasH;

        byte[] bytes = new byte[atlasW * atlasH];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(atlas[i]) * 255f);

        Texture2D fresh = new(atlasW, atlasH, TextureFormat.Alpha8, false)
        {
            name = texture.name,
            filterMode = texture.filterMode,
            wrapMode = texture.wrapMode,
            anisoLevel = texture.anisoLevel,
        };
        fresh.SetPixelData(bytes, 0);
        fresh.Apply(false, false);

        // Copy into the existing texture object, so nothing pointing at it breaks
        EditorUtility.CopySerialized(fresh, texture);

        if (WroteCorrectly(texture))
        {
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(texture);
        }
        else
        {
            // Fallback: swap the texture inside the font asset for the new one
            AssetDatabase.AddObjectToAsset(fresh, font);

            SerializedObject swap = new(font);
            SerializedProperty textures = swap.FindProperty("m_AtlasTextures");
            if (textures.arraySize == 0) textures.arraySize = 1;
            textures.GetArrayElementAtIndex(0).objectReferenceValue = fresh;
            swap.ApplyModifiedPropertiesWithoutUndo();

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_MainTex") || material.GetTexture("_MainTex") != texture) continue;
                material.SetTexture("_MainTex", fresh);
                EditorUtility.SetDirty(material);
            }

            if (font.material != null) font.material.SetTexture("_MainTex", fresh);

            AssetDatabase.RemoveObjectFromAsset(texture);
            Object.DestroyImmediate(texture, true);
            texture = fresh;

            Debug.Log($"{font.name}: atlas texture replaced (in-place copy did not take).", font);
        }

        if (!resized) return;

        // A taller atlas: TMP works out UVs from these
        SerializedObject so = new(font);
        so.FindProperty("m_AtlasWidth").intValue = atlasW;
        so.FindProperty("m_AtlasHeight").intValue = atlasH;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || !material.HasProperty("_MainTex") || material.GetTexture("_MainTex") != texture) continue;

            material.SetFloat("_TextureWidth", atlasW);
            material.SetFloat("_TextureHeight", atlasH);
            EditorUtility.SetDirty(material);
        }

        // The font's own material lives inside the font asset
        if (font.material != null)
        {
            font.material.SetFloat("_TextureWidth", atlasW);
            font.material.SetFloat("_TextureHeight", atlasH);
            EditorUtility.SetDirty(font.material);
        }
    }
}

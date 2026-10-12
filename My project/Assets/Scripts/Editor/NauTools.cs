using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only tools for the Nau project. Everything goes through the real API
// so the 7 MB scene file is never edited by hand.
//
// The performance items are deliberately separate menu entries: measure with
// OVR Metrics Tool on device between each one, or you learn nothing about
// which change actually paid off.
public static class NauTools
{
    // Nothing on this ship needs 5 km of trees - the player is on a deck.
    private const float TreeDistance = 150f;

    [MenuItem("Nau/Performance/0. Report", priority = 0)]
    private static void Report()
    {
        Animator[] animators = FindAll<Animator>();
        Terrain[] terrains = FindAll<Terrain>();

        int alwaysAnimate = 0;
        foreach (Animator a in animators)
        {
            if (a.cullingMode == AnimatorCullingMode.AlwaysAnimate) alwaysAnimate++;
        }

        int notInstanced = 0;
        foreach (Terrain t in terrains)
        {
            if (!t.drawInstanced) notInstanced++;
        }

        Debug.Log(
            $"Nau perf report\n" +
            $"  Animators: {animators.Length} total, {alwaysAnimate} still Always Animate\n" +
            $"  Skinned meshes: {FindAll<SkinnedMeshRenderer>().Length}\n" +
            $"  Terrains: {terrains.Length} total, {notInstanced} without instancing\n" +
            $"  Android graphics API: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
    }

    // Skinned meshes are the expensive half: an Always Animate rig keeps
    // sampling curves and re-skinning even when it is behind you.
    //
    // Cull Completely freezes the state machine off-screen, so anything that
    // must keep time while unseen (root motion, a timed sequence) has to stay
    // on Always Animate. The sailors are looping idles, so they are fine.
    [MenuItem("Nau/Performance/1. Cull Animators Offscreen", priority = 1)]
    private static void CullAnimators()
    {
        Animator[] animators = FindAll<Animator>();
        List<Animator> changed = new();

        foreach (Animator a in animators)
        {
            if (a.cullingMode != AnimatorCullingMode.AlwaysAnimate) continue;
            changed.Add(a);
        }

        if (changed.Count == 0)
        {
            Debug.Log("Every animator is already culled.");
            return;
        }

        Undo.RecordObjects(changed.ToArray(), "Cull Animators Offscreen");

        foreach (Animator a in changed)
        {
            a.cullingMode = AnimatorCullingMode.CullCompletely;
            EditorUtility.SetDirty(a);
        }

        MarkSceneDirty();
        Debug.Log($"Set {changed.Count} animators to Cull Completely. Save the scene.");
    }

    [MenuItem("Nau/Performance/2. Optimize Terrains", priority = 2)]
    private static void OptimizeTerrains()
    {
        Terrain[] terrains = FindAll<Terrain>();

        if (terrains.Length == 0)
        {
            Debug.Log("No terrains in the open scene.");
            return;
        }

        Undo.RecordObjects(terrains, "Optimize Terrains");

        foreach (Terrain t in terrains)
        {
            t.drawInstanced = true;
            t.treeDistance = TreeDistance;
            EditorUtility.SetDirty(t);
        }

        MarkSceneDirty();
        Debug.Log($"{terrains.Length} terrains: instancing on, tree distance {TreeDistance} m. Save the scene.");
    }

    // Meta's recommendation on Quest 3, and foveated rendering will not work
    // without it. Make a build and check for shader oddities before trusting it.
    [MenuItem("Nau/Performance/3. Use Vulkan On Android", priority = 3)]
    private static void UseVulkan()
    {
        GraphicsDeviceType[] current = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);

        if (current.Length == 1 && current[0] == GraphicsDeviceType.Vulkan)
        {
            Debug.Log("Android is already Vulkan only.");
            return;
        }

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        Debug.Log($"Android graphics API: {string.Join(", ", current)} -> Vulkan. Rebuild and test on device.");
    }

    [MenuItem("Nau/Performance/Revert To OpenGLES3", priority = 20)]
    private static void UseGLES()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

        Debug.Log("Android graphics API back to OpenGLES3.");
    }

    // Effect packs ship their systems looping and playing on awake, which is
    // right for a showcase scene and wrong for anything a script triggers.
    // Looping is per-system and a single effect is often eight of them.
    [MenuItem("Nau/Effects/Make Selection One-Shot")]
    private static void MakeOneShot()
    {
        int changed = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                Undo.RecordObject(system, "Make One-Shot");

                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                main.prewarm = false;
                main.playOnAwake = false;

                EditorUtility.SetDirty(system);
                changed++;
            }
        }

        Debug.Log(changed == 0
            ? "Select an effect in the Hierarchy or Project first."
            : $"{changed} particle system(s) set to one-shot: no loop, no prewarm, no play on awake.");
    }

    // Exact triangle cost of the open scene: every placed copy of every mesh,
    // grouped by mesh and by area of the ship. A file-level count cannot do
    // this - one FBX often holds dozens of pieces and only some are placed.
    [MenuItem("Nau/Performance/Report Scene Triangles")]
    private static void ReportSceneTriangles()
    {
        Dictionary<Mesh, (int copies, int tris, bool skinned)> byMesh = new();
        Dictionary<string, long> byArea = new();
        long total = 0, skinnedTotal = 0;

        void Add(Mesh mesh, Transform at, bool skinned)
        {
            if (mesh == null) return;

            int tris = Triangles(mesh);

            byMesh[mesh] = byMesh.TryGetValue(mesh, out var row)
                ? (row.copies + 1, tris, row.skinned || skinned)
                : (1, tris, skinned);

            string area = AreaOf(at);
            byArea[area] = (byArea.TryGetValue(area, out long sum) ? sum : 0) + tris;

            total += tris;
            if (skinned) skinnedTotal += tris;
        }

        // Only meshes that are actually drawn - a MeshFilter feeding just a
        // collider costs nothing on screen
        foreach (MeshFilter filter in FindAll<MeshFilter>())
        {
            if (filter.GetComponent<MeshRenderer>() != null)
                Add(filter.sharedMesh, filter.transform, false);
        }

        foreach (SkinnedMeshRenderer skin in FindAll<SkinnedMeshRenderer>())
            Add(skin.sharedMesh, skin.transform, true);

        List<KeyValuePair<Mesh, (int copies, int tris, bool skinned)>> meshes = new(byMesh);
        meshes.Sort((a, b) => ((long)b.Value.copies * b.Value.tris).CompareTo((long)a.Value.copies * a.Value.tris));

        System.Text.StringBuilder report = new();
        report.AppendLine($"Scene triangles: {total:N0} in total, {skinnedTotal:N0} of them skinned (sailors - skinning costs CPU on top).");
        report.AppendLine("Levels are counted even while switched off, so read L1-L4 one at a time, not added together.\n");

        report.AppendLine("Worst meshes (copies x triangles):");
        for (int i = 0; i < Mathf.Min(30, meshes.Count); i++)
        {
            Mesh mesh = meshes[i].Key;
            var row = meshes[i].Value;
            string path = AssetDatabase.GetAssetPath(mesh);
            report.AppendLine($"  {(long)row.copies * row.tris,10:N0}  = {row.tris,7:N0} x {row.copies,-3} {mesh.name}{(row.skinned ? "  [skinned]" : "")}  ({(string.IsNullOrEmpty(path) ? "scene-only" : path)})");
        }

        List<KeyValuePair<string, long>> areas = new(byArea);
        areas.Sort((a, b) => b.Value.CompareTo(a.Value));

        report.AppendLine("\nBy area:");
        foreach (var area in areas)
            report.AppendLine($"  {area.Value,10:N0}  {area.Key}");

        Debug.Log(report.ToString());
    }

    private static int Triangles(Mesh mesh)
    {
        // GetIndexCount works on meshes that are not Read/Write enabled
        long count = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                count += mesh.GetIndexCount(i) / 3;
        }

        return (int)count;
    }

    // "ALL/Props", "ALL/Sailors", "ALL/L3 (Cannons)"... for anything on the
    // ship, so the props question can be answered directly; the scene root
    // for everything else
    private static string AreaOf(Transform t)
    {
        Transform root = t.root;

        if (root.GetComponent<BoatWiggle>() == null)
            return root.name;

        Transform area = t;
        while (area.parent != null && area.parent != root) area = area.parent;

        return area == root ? root.name : $"{root.name}/{area.name}";
    }

    // Builds a cheap storm cloud from nothing: a generated puff texture, an
    // unlit alpha-blended particle material, and ~30 large slow billboards.
    // Made as real assets rather than at runtime, so the shader variant is
    // referenced and survives build stripping.
    [MenuItem("Nau/Effects/Create Storm Cloud")]
    private static void CreateStormCloud()
    {
        const string folder = "Assets/Effects/StormCloud";
        System.IO.Directory.CreateDirectory(folder);

        Texture2D puff = MakeCloudTexture($"{folder}/CloudPuff.png");
        Material material = MakeCloudMaterial($"{folder}/CloudPuff.mat", puff);
        if (material == null) return;

        GameObject go = new("Storm Cloud");
        Undo.RegisterCreatedObjectUndo(go, "Create Storm Cloud");

        // Under whatever is selected - the L3 container, so it only shows
        // during the cannon level and rides the ship with the player
        if (Selection.activeTransform != null)
        {
            go.transform.SetParent(Selection.activeTransform, false);
            go.transform.localPosition = Vector3.up * 22f;
        }

        ParticleSystem system = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;                 // already full when the level starts
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(12f, 20f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.30f, 0.31f, 0.35f, 1f), new Color(0.45f, 0.46f, 0.50f, 1f));
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 2.5f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 3f, 30f);

        // Fade in and out, so puffs never pop
        Gradient fade = new();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        colour.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 1.1f));

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // All three axes must share a mode, or Unity rejects the module
        ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.Local;
        drift.x = new ParticleSystem.MinMaxCurve(0.25f);
        drift.y = new ParticleSystem.MinMaxCurve(0f);
        drift.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 0.6f;     // caps overdraw when looking straight up

        // One big soft shape under the puffs. Fills every gap between them for
        // the price of a single draw - far cheaper than adding particles
        Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/CloudBase.mat");
        if (baseMaterial == null)
        {
            baseMaterial = new Material(material);
            AssetDatabase.CreateAsset(baseMaterial, $"{folder}/CloudBase.mat");
        }

        baseMaterial.CopyPropertiesFromMaterial(material);
        baseMaterial.SetColor("_BaseColor", new Color(0.30f, 0.31f, 0.35f, 0.9f));
        if (baseMaterial.HasProperty("_Cull")) baseMaterial.SetFloat("_Cull", 0f);   // seen from below
        EditorUtility.SetDirty(baseMaterial);

        GameObject underside = GameObject.CreatePrimitive(PrimitiveType.Quad);
        underside.name = "Cloud Base";
        Object.DestroyImmediate(underside.GetComponent<Collider>());
        underside.transform.SetParent(go.transform, false);
        underside.transform.localPosition = Vector3.down * 1.5f;
        underside.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        underside.transform.localScale = new Vector3(38f, 38f, 1f);

        MeshRenderer undersideRenderer = underside.GetComponent<MeshRenderer>();
        undersideRenderer.sharedMaterial = baseMaterial;
        undersideRenderer.shadowCastingMode = ShadowCastingMode.Off;
        undersideRenderer.receiveShadows = false;

        go.AddComponent<StormCloud>();
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = go;
        MarkSceneDirty();
        Debug.Log($"Storm cloud created with its texture and material in {folder}. Move it over the cannon deck.", go);
    }

    // Dusty air for below deck: small clouds of dust puffing up off the floor
    // and settling, plus specks that wink in, wander and wink out.
    //
    // The two systems are found by name (Ground Haze / Dust Motes), so
    // Update Below-Deck Dust can restyle copies already placed in the scene
    // without losing the room sizes set on them.
    private const string DustFolder = "Assets/Effects/StormCloud";
    private const string HazeName = "Ground Haze";
    private const string MotesName = "Dust Motes";

    [MenuItem("Nau/Effects/Create Below-Deck Dust")]
    private static void CreateBelowDeckDust()
    {
        if (!DustMaterials(out Material speck, out Material cloud)) return;

        GameObject root = new("Below-Deck Dust");
        Undo.RegisterCreatedObjectUndo(root, "Create Below-Deck Dust");
        if (Selection.activeTransform != null) root.transform.SetParent(Selection.activeTransform, false);

        ConfigureDustClouds(NewParticleChild(HazeName, root.transform), cloud, new Vector2(12f, 12f));
        ConfigureDustMotes(NewParticleChild(MotesName, root.transform), speck, new Vector2(12f, 12f));

        Selection.activeGameObject = root;
        MarkSceneDirty();
        Debug.Log("Below-deck dust created. Set its position on the floor of the hold and size the X and Z of the two Shape boxes to the room.", root);
    }

    // Restyles every Ground Haze / Dust Motes under the selection - or in the
    // whole scene if nothing is selected. Keeps each one's X/Z room size.
    [MenuItem("Nau/Effects/Update Below-Deck Dust")]
    private static void UpdateBelowDeckDust()
    {
        if (!DustMaterials(out Material speck, out Material cloud)) return;

        List<ParticleSystem> systems = new();

        if (Selection.gameObjects.Length > 0)
        {
            foreach (GameObject go in Selection.gameObjects)
                systems.AddRange(go.GetComponentsInChildren<ParticleSystem>(true));
        }
        else
        {
            systems.AddRange(Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        int updated = 0;

        foreach (ParticleSystem system in systems)
        {
            bool haze = system.name.StartsWith(HazeName);
            bool motes = system.name.StartsWith(MotesName);
            if (!haze && !motes) continue;

            Undo.RecordObject(system, "Update Below-Deck Dust");
            Undo.RecordObject(system.transform, "Update Below-Deck Dust");
            Undo.RecordObject(system.GetComponent<ParticleSystemRenderer>(), "Update Below-Deck Dust");

            Vector3 box = system.shape.scale;
            Vector2 room = new(box.x, box.z);

            if (haze) ConfigureDustClouds(system, cloud, room);
            else ConfigureDustMotes(system, speck, room);

            EditorUtility.SetDirty(system);
            updated++;
        }

        MarkSceneDirty();
        Debug.Log(updated > 0
            ? $"Updated {updated} dust systems. Room sizes kept."
            : "No Ground Haze or Dust Motes found - select the Below-Deck Dust objects, or select nothing to search the whole scene.");
    }

    private static bool DustMaterials(out Material speck, out Material cloud)
    {
        System.IO.Directory.CreateDirectory(DustFolder);

        // Specks: a plain soft dot. Clouds: the lumpy cloud texture, a bit
        // softer than the storm one, so each puff has a ragged edge
        speck = MakeCloudMaterial($"{DustFolder}/DustPuff.mat", MakeCloudTexture($"{DustFolder}/DustPuff.png", 1.0f, 0.9f));
        cloud = MakeCloudMaterial($"{DustFolder}/DustCloud.mat", MakeCloudTexture($"{DustFolder}/DustCloud.png", 1.2f, 1.0f));

        return speck != null && cloud != null;
    }

    private static ParticleSystem NewParticleChild(string name, Transform parent)
    {
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<ParticleSystem>();
    }

    // Small clouds rather than a sheet: camera-facing puffs a little off the
    // floor, each swelling as it rises and thinning out as it settles. No soft
    // particles on Quest, so they are kept high enough and faint enough at
    // the edge that the line where a puff meets the floor does not show.
    private static void ConfigureDustClouds(ParticleSystem system, Material material, Vector2 room)
    {
        system.transform.localPosition = Vector3.up * 0.5f;

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.50f, 0.42f, 0.14f), new Color(0.62f, 0.56f, 0.46f, 0.22f));
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 3f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(room.x, 0.25f, room.y);

        // Quick puff up, long settle
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        colour.color = Fade(0.15f, 0.5f);

        // Starts as a tight puff and spreads out
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f, 0f, 2.5f), new Keyframe(1f, 1.5f, 0.3f, 0f)));

        // A slow roll, so a puff never sits there like a sticker
        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.Local;
        drift.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        drift.y = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        drift.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 0.5f;
    }

    // Specks that come and go: short lives with a quick wink in and out, and
    // a little low-quality noise so each one wanders instead of gliding in a
    // straight line. ~60 alive at once, cheap enough for Quest.
    private static void ConfigureDustMotes(ParticleSystem system, Material material, Vector2 room)
    {
        system.transform.localPosition = Vector3.up * 1.4f;

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startRotation = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.8f, 0.35f), new Color(1f, 0.95f, 0.8f, 0.75f));
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(room.x, 2.5f, room.y);

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        colour.color = Fade(0.2f, 0.75f);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = false;

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = false;

        // Base drift, slightly upward like warm air
        ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.Local;
        drift.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        drift.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.05f);
        drift.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        // The wander. Low quality is the cheapest noise - fine for 80 dots
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.separateAxes = false;
        noise.strength = 0.2f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.maxParticleSize = 0.5f;
    }

    // The cannon target's hit effect: a burst of dust that punches out, slows
    // as if the air catches it, swells and fades away. Three layers:
    //   Dust Burst  - the main cloud (the root; destroys the effect when done)
    //   Flash Puff  - a quick pale pop in the middle, so the hit reads at once
    //   Grit        - small dark bits thrown out that fall back down
    // Built as a prefab and plugged into every TargetHit in the open scene.
    // Extra pieces (flying wood) go in as more children of the prefab - keep
    // their lifetimes under Dust Burst's, which is the one that cleans up.
    private const string ExplosionPath = "Assets/Effects/TargetExplosion/TargetDustExplosion.prefab";

    [MenuItem("Nau/Effects/Create Target Dust Explosion")]
    private static void CreateTargetExplosion()
    {
        if (!DustMaterials(out Material speck, out Material cloud)) return;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ExplosionPath));
        AssetDatabase.Refresh();   // the prefab save needs the folder imported

        GameObject root = new("TargetDustExplosion");

        // Main cloud - the root, so its Stop Action removes the whole effect
        ParticleSystem dust = root.AddComponent<ParticleSystem>();
        ConfigureBurst(dust, cloud, count: 18, speed: new(3f, 7f), size: new(0.8f, 1.6f), life: new(1.6f, 2.6f),
            colourA: new Color(0.58f, 0.51f, 0.42f, 0.85f), colourB: new Color(0.70f, 0.63f, 0.52f, 0.85f), radius: 0.3f);
        {
            ParticleSystem.MainModule main = dust.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.gravityModifier = -0.05f;   // drifts up a touch as it hangs

            // Shoots out, then the air catches it
            ParticleSystem.LimitVelocityOverLifetimeModule air = dust.limitVelocityOverLifetime;
            air.enabled = true;
            air.limit = 100f;
            air.dampen = 0f;
            air.drag = 2.5f;
            air.multiplyDragByParticleSize = false;

            // Swells as it slows
            ParticleSystem.SizeOverLifetimeModule grow = dust.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f, 0f, 3f), new Keyframe(0.35f, 1.3f), new Keyframe(1f, 1.8f, 0.4f, 0f)));

            // Pops in, thins out, gone - and greys a little as it goes
            Gradient fade = new();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f),
                        new GradientAlphaKey(0.7f, 0.45f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colour = dust.colorOverLifetime;
            colour.enabled = true;
            colour.color = fade;

            ParticleSystem.RotationOverLifetimeModule spin = dust.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        }

        // Pale pop in the middle
        ParticleSystem flash = NewParticleChild("Flash Puff", root.transform);
        ConfigureBurst(flash, cloud, count: 6, speed: new(0.5f, 1.5f), size: new(1.0f, 1.8f), life: new(0.35f, 0.6f),
            colourA: new Color(0.92f, 0.85f, 0.70f, 0.9f), colourB: new Color(0.98f, 0.92f, 0.78f, 0.9f), radius: 0.15f);
        {
            ParticleSystem.SizeOverLifetimeModule grow = flash.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f, 0f, 3f), new Keyframe(1f, 1.4f, 0.3f, 0f)));

            ParticleSystem.ColorOverLifetimeModule colour = flash.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.05f, 0.3f);
        }

        // Dark grit thrown out and falling back
        ParticleSystem grit = NewParticleChild("Grit", root.transform);
        ConfigureBurst(grit, speck, count: 30, speed: new(4f, 9f), size: new(0.04f, 0.1f), life: new(0.6f, 1.2f),
            colourA: new Color(0.25f, 0.20f, 0.15f, 1f), colourB: new Color(0.40f, 0.32f, 0.24f, 1f), radius: 0.2f);
        {
            ParticleSystem.MainModule main = grit.main;
            main.gravityModifier = 1f;

            ParticleSystem.LimitVelocityOverLifetimeModule air = grit.limitVelocityOverLifetime;
            air.enabled = true;
            air.limit = 100f;
            air.dampen = 0f;
            air.drag = 0.6f;
            air.multiplyDragByParticleSize = false;

            ParticleSystem.ColorOverLifetimeModule colour = grit.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.01f, 0.7f);

            grit.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.None;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPath);
        Object.DestroyImmediate(root);

        // Plug it into every target in the open scene
        int wired = 0;
        foreach (TargetHit target in FindAll<TargetHit>())
        {
            Undo.RecordObject(target, "Assign Target Explosion");

            if (target.hitEffectPrefab != null && target.hitEffectPrefab != prefab)
                Debug.Log($"{target.name}: replaced hit effect '{target.hitEffectPrefab.name}' with the dust explosion.", target);

            target.hitEffectPrefab = prefab;
            EditorUtility.SetDirty(target);
            wired++;
        }

        if (wired > 0) MarkSceneDirty();

        Selection.activeObject = prefab;
        Debug.Log($"Target dust explosion saved at {ExplosionPath} and assigned to {wired} target(s). " +
                  "Scale the prefab root to make the whole thing bigger or smaller.", prefab);
    }

    // A one-shot burst: everything leaves at once from a small sphere, plays
    // as soon as it is spawned, never loops. Scales with its transform.
    private static void ConfigureBurst(ParticleSystem system, Material material, int count, Vector2 speed, Vector2 size,
        Vector2 life, Color colourA, Color colourB, float radius)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = true;
        main.duration = life.y;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // rides the ship like the targets
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 0.8f;
    }

    // Alpha 0 -> 1 by fadeIn, holds until fadeOut, then back to 0
    private static Gradient Fade(float fadeIn, float fadeOut)
    {
        Gradient fade = new();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, fadeOut), new GradientAlphaKey(0f, 1f) });
        return fade;
    }

    // solidity: how far out the puff stays opaque. density: overall opacity.
    // Cloud defaults are dense and lumpy; dust wants both lower, for a haze.
    private static Texture2D MakeCloudTexture(string path, float solidity = 1.6f, float density = 1.4f)
    {
        const int size = 128;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                // Solid out to about 60% of the radius, soft only at the rim - a
                // thin fringe is what made overlapping puffs look full of holes
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * solidity));

                // A few octaves of noise for a lumpy, cauliflower edge
                float noise = 0f, amplitude = 0.5f, frequency = 0.035f;
                for (int octave = 0; octave < 4; octave++)
                {
                    noise += Mathf.PerlinNoise(x * frequency + 13.7f, y * frequency + 41.3f) * amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }

                float alpha = Mathf.Clamp01(edge * (0.8f + 0.6f * (noise - 0.45f)) * density);
                byte shade = (byte)(Mathf.Lerp(0.7f, 1f, noise) * 255f);
                pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeCloudMaterial(string path, Texture2D puff)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError("URP Particles/Unlit shader not found.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", puff);
        material.SetColor("_BaseColor", Color.white);

        // Alpha-blended transparent - what the URP inspector would set
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        // Needs the depth texture, which the Quest pipeline does not have
        material.SetFloat("_SoftParticlesEnabled", 0f);
        material.DisableKeyword("_SOFTPARTICLES_ON");

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // Ranks every particle system under the selection by what actually costs
    // frames on Quest, so the worst offender is first rather than a guess.
    [MenuItem("Nau/Effects/Report Selection Cost")]
    private static void ReportParticleCost()
    {
        List<(int score, string line)> rows = new();

        foreach (GameObject go in Selection.gameObjects)
        {
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                List<string> flags = new();
                int score = 0;

                int max = system.main.maxParticles;
                score += max / 50;

                // Each of these is per particle, per frame, on the CPU
                if (system.collision.enabled)
                {
                    score += system.collision.type == ParticleSystemCollisionType.World ? 30 : 10;
                    flags.Add($"collision:{system.collision.type}/{system.collision.quality}");
                }

                // A realtime light per particle - brutal on mobile
                if (system.lights.enabled) { score += 40; flags.Add("LIGHTS"); }
                if (system.noise.enabled) { score += 5; flags.Add($"noise:{system.noise.quality}"); }
                if (system.trails.enabled) { score += 10; flags.Add("trails"); }
                if (system.subEmitters.enabled) { score += 5; flags.Add("sub-emitters"); }

                if (renderer != null)
                {
                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    {
                        score += 15;
                        flags.Add("casts shadows");
                    }

                    Material material = renderer.sharedMaterial;

                    if (material != null && material.shader != null && material.shader.name.Contains("Lit") && !material.shader.name.Contains("Unlit"))
                    {
                        score += 8;
                        flags.Add("lit shader");
                    }

                    if (material != null && material.HasProperty("_SoftParticlesEnabled") && material.GetFloat("_SoftParticlesEnabled") > 0f)
                        flags.Add("soft particles (invisible on Quest)");

                    // Big particles near the camera are the overdraw killer
                    if (renderer.maxParticleSize > 0.5f)
                    {
                        score += 10;
                        flags.Add($"max size {renderer.maxParticleSize:0.##}");
                    }
                }

                rows.Add((score, $"{score,4}  {system.name}  max {max}  {string.Join(", ", flags)}"));
            }
        }

        if (rows.Count == 0)
        {
            Debug.Log("Select an effect, or a parent of several, first.");
            return;
        }

        rows.Sort((a, b) => b.score.CompareTo(a.score));

        Debug.Log("Particle cost, worst first (score is rough):\n" +
                  string.Join("\n", rows.ConvertAll(r => r.line)));
    }

    // Only changes that do not alter how the effect reads: shadows, lights,
    // collision and noise quality, and offscreen culling. Counts and sizes are
    // left alone, since those are the look - the report says where to cut.
    [MenuItem("Nau/Effects/Optimize Selection For Quest")]
    private static void OptimizeParticles()
    {
        int changed = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                Undo.RecordObject(system, "Optimize For Quest");

                ParticleSystem.MainModule main = system.main;
                main.cullingMode = ParticleSystemCullingMode.Automatic;

                ParticleSystem.CollisionModule collision = system.collision;
                if (collision.enabled)
                {
                    collision.quality = ParticleSystemCollisionQuality.Low;
                    collision.enableDynamicColliders = false;
                }

                ParticleSystem.LightsModule lights = system.lights;
                lights.enabled = false;

                ParticleSystem.NoiseModule noise = system.noise;
                if (noise.enabled) noise.quality = ParticleSystemNoiseQuality.Low;

                EditorUtility.SetDirty(system);

                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Optimize For Quest");
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    EditorUtility.SetDirty(renderer);
                }

                changed++;
            }
        }

        Debug.Log(changed == 0
            ? "Select an effect first."
            : $"{changed} particle system(s): shadows off, particle lights off, collision and noise to low quality, offscreen culling on.");
    }

    // Soft particles need the camera depth texture, which the Mobile pipeline
    // asset does not provide - so they simply vanish on the Quest.
    [MenuItem("Nau/Effects/Disable Soft Particles On Selection")]
    private static void DisableSoftParticles()
    {
        int changed = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_SoftParticlesEnabled")) continue;
                    if (material.GetFloat("_SoftParticlesEnabled") == 0f) continue;

                    Undo.RecordObject(material, "Disable Soft Particles");

                    material.SetFloat("_SoftParticlesEnabled", 0f);
                    material.DisableKeyword("_SOFTPARTICLES_ON");

                    EditorUtility.SetDirty(material);
                    changed++;
                }
            }
        }

        Debug.Log(changed == 0
            ? "Nothing selected had soft particles switched on."
            : $"Soft particles switched off on {changed} material(s). These edit the material asset, so every user of it changes.");
    }

    // The editor runs the PC quality level, the headset runs Mobile - which is
    // why the headset can look nothing like the scene view.
    [MenuItem("Nau/Look/Use Mobile Quality In Editor")]
    private static void UseMobileQuality()
    {
        string[] names = QualitySettings.names;

        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] != "Mobile") continue;

            QualitySettings.SetQualityLevel(i, true);
            Debug.Log("Editor now renders with the Mobile quality level (LDR, same grading path as the Quest).");
            return;
        }

        Debug.LogError("No quality level named 'Mobile' found.");
    }

    [MenuItem("Nau/Settings/Clear Saved Settings")]
    private static void ClearPrefs()
    {
        PlayerPrefs.DeleteKey("nau.music");
        PlayerPrefs.DeleteKey("nau.sfx");
        PlayerPrefs.DeleteKey("nau.height");
        PlayerPrefs.DeleteKey("nau.textScale");
        PlayerPrefs.DeleteKey("nau.language");
        PlayerPrefs.Save();

        Debug.Log("Saved settings cleared.");
    }

    // The accents were stripped from every text back when the font had no
    // accented letters. Nau/Fonts/Add Portuguese Accents gave it them, so this
    // puts the correct spelling back - LocalizedText fields, typed TMP labels
    // and spice descriptions, in the open scene and in every prefab.
    //
    // A list of exact fixes rather than a guess: "e" vs "é" or "esta" vs
    // "está" cannot be told apart automatically. Safe to run twice.
    private static readonly (string pattern, string replacement, bool portugueseOnly)[] AccentFixes =
    {
        // Whole sentences first - wording fixes as well as accents
        (@"Tem uma forma estranha e sao castanhos", "Tem uma forma estranha e é castanho", true),
        (@"Acho que tem umas vermelhinhas", "Acho que há umas vermelhinhas", true),
        (@"Traz-me ca uma", "Traz-me cá uma", true),
        (@"canela, pa\b", "canela, pá", true),
        (@"^E como a pimenta", "É como a pimenta", true),
        (@"(\n)E como a pimenta", "$1É como a pimenta", true),
        (@"\bate\b", "até", true),

        // Single words - none of these are English, so labels are safe too
        (@"\bNivel\b", "Nível", false),
        (@"\bMusica\b", "Música", false),
        (@"\bmusica\b", "música", false),
        (@"\bRecomecar\b", "Recomeçar", false),
        (@"\bComecar\b", "Começar", false),
        (@"\bcanhao\b", "canhão", false),
        (@"\bCanhao\b", "Canhão", false),
        (@"\bgavia\b", "gávea", false),
        (@"\bOpcoes\b", "Opções", false),
        (@"\bDefinicoes\b", "Definições", false),
        (@"\bPortugues\b", "Português", false),
        (@"\bsao\b", "são", false),
        (@"\bSao\b", "São", false),
        (@"\bnao\b", "não", false),
        (@"\bNao\b", "Não", false),
        (@"\bpolvora\b", "pólvora", false),
        (@"\bnavegacao\b", "navegação", false),
        (@"\bpassaros\b", "pássaros", false),
        (@"\bbussola\b", "bússola", false),
        (@"\bastrolabio\b", "astrolábio", false),
    };

    [MenuItem("Nau/Settings/Restore Portuguese Accents")]
    private static void RestoreAccents()
    {
        System.Text.StringBuilder log = new();
        int changed = 0;

        // Prefabs first, so scene copies of them pick the fix up from the source
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Samples/")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int inPrefab = 0;

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                inPrefab += RestoreStrings(behaviour, log);

            if (inPrefab > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            changed += inPrefab;
        }

        int inScene = 0;
        foreach (MonoBehaviour behaviour in FindAll<MonoBehaviour>())
            inScene += RestoreStrings(behaviour, log);

        if (inScene > 0) MarkSceneDirty();
        changed += inScene;

        Debug.Log(changed == 0
            ? "Nothing to restore - the text already has its accents."
            : $"Restored accents in {changed} text field(s). Save the scene.\n{log}");
    }

    private static int RestoreStrings(MonoBehaviour behaviour, System.Text.StringBuilder log)
    {
        if (behaviour == null) return 0;

        SerializedObject serialized = new(behaviour);
        int changed = 0;

        // The Explore button had its two languages the wrong way round
        if (behaviour is LocalizedText localized && localized.portuguese == "Explore" && localized.english == "Explorar")
        {
            serialized.FindProperty("portuguese").stringValue = "Explorar";
            serialized.FindProperty("english").stringValue = "Explore";
            log.AppendLine($"  {behaviour.name}: swapped Explore / Explorar");
            changed++;
        }

        SerializedProperty property = serialized.GetIterator();

        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.String) continue;

            // Only Portuguese fields get the sentence fixes; typed labels
            // (m_text) only get the words that cannot be English
            bool portuguese = property.name is "portuguese" or "itemNameText" or "descriptionText";
            bool label = property.name == "m_text";
            if (!portuguese && !label) continue;

            string text = property.stringValue;
            if (string.IsNullOrEmpty(text)) continue;

            string fixedText = text;
            foreach (var (pattern, replacement, portugueseOnly) in AccentFixes)
            {
                if (portugueseOnly && !portuguese) continue;
                fixedText = System.Text.RegularExpressions.Regex.Replace(fixedText, pattern, replacement);
            }

            if (fixedText == text) continue;

            log.AppendLine($"  {behaviour.name} ({behaviour.GetType().Name}.{property.name}): \"{text}\" -> \"{fixedText}\"");
            property.stringValue = fixedText;
            changed++;
        }

        if (changed > 0) serialized.ApplyModifiedProperties();
        return changed;
    }

    // Puts every star back to unearned - the only way to test a first finish twice
    [MenuItem("Nau/Settings/Clear Puzzle Records")]
    private static void ClearRecords()
    {
        PuzzleRecords.Clear();
        Debug.Log("Puzzle best times cleared.");
    }

    private static T[] FindAll<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static void MarkSceneDirty()
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}

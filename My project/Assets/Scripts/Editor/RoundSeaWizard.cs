using System.Collections.Generic;
using Bitgem.VFX.StylisedWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Swaps the Bitgem water's square mesh for a round one, so there are no
// corners on the horizon - the edge is the same distance away in every
// direction, which lets the fog hide it evenly.
//
// The water shader makes the waves by moving the vertices, and it was tuned
// for Bitgem's tile grid. So the round sea is built on that SAME grid - same
// spacing, same line positions - and only trimmed to a circle, with the edge
// points pulled onto it. Anything denser shows the wave maths as spikes; a
// radial mesh pinches every wave into the centre.
//
// The Bitgem component rebuilds its square on its first Update (in builds
// too), so it is switched OFF here. To get the square back, tick it on again.
public class RoundSeaWizard : ScriptableWizard
{
    [Tooltip("The Water object - it has the Mesh Filter and the Bitgem water volume.")]
    public MeshFilter water;

    [Tooltip("Radius of the sea in metres. The default covers everything the square covered, corners included.")]
    public float radius = 800f;

    [Tooltip("Grid spacing in the water's local units. Read from the Bitgem volume's Tile Size - keep it the same, or the waves change.")]
    public float tileSize = 2.8f;

    private const string MeshPath = "Assets/Effects/Ocean/RoundSea.asset";

    [MenuItem("Nau/Ocean/Make Sea Round")]
    private static void Open()
    {
        RoundSeaWizard wizard = DisplayWizard<RoundSeaWizard>("Make Sea Round", "Make Round");

        WaterVolumeBase volume = Object.FindAnyObjectByType<WaterVolumeBase>(FindObjectsInactive.Include);

        if (volume != null)
        {
            wizard.water = volume.GetComponent<MeshFilter>();
            wizard.tileSize = volume.TileSize;
        }

        wizard.radius = wizard.DefaultRadius();
        wizard.OnWizardUpdate();
    }

    private void OnWizardUpdate()
    {
        isValid = water != null && water.sharedMesh != null && radius > 0f && tileSize > 0f;
        helpString = water == null
            ? "Drag the Water object (the one with the Bitgem water volume) into Water."
            : $"Makes a {radius:0} m round sea on Bitgem's {tileSize} grid, centred where the square was. The Bitgem water volume on '{water.name}' is switched off so it does not rebuild the square. Undo works.";
    }

    // Half the diagonal of the current surface - a circle through its corners
    private float DefaultRadius()
    {
        if (water == null || water.sharedMesh == null) return radius;

        Bounds bounds = water.sharedMesh.bounds;
        Vector3 scale = water.transform.lossyScale;

        // Already round: the bounds are the square around the circle
        if (water.sharedMesh.name.StartsWith("RoundSea"))
            return Mathf.Round(bounds.extents.x * Mathf.Abs(scale.x));

        float halfX = bounds.extents.x * Mathf.Abs(scale.x);
        float halfZ = bounds.extents.z * Mathf.Abs(scale.z);
        return Mathf.Ceil(Mathf.Sqrt(halfX * halfX + halfZ * halfZ));
    }

    private void OnWizardCreate()
    {
        Transform t = water.transform;
        Mesh old = water.sharedMesh;

        // Surface height: the highest vertex of the current mesh (its top faces)
        float top = 0f;
        if (old.vertexCount > 0)
        {
            top = float.MinValue;
            foreach (Vector3 v in old.vertices) top = Mathf.Max(top, v.y);
        }

        Vector3 centre = old.bounds.center;

        // The object is scaled differently on X and Z, so a circle in the world
        // is an ellipse in its local space
        Vector3 scale = t.lossyScale;
        float rx = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float rz = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.z));

        // Bitgem's grid lines sit at k * TileSize - 0.5 - keep them exactly
        // there, so the waves near the ship are identical to before
        int minX = Mathf.FloorToInt((centre.x - rx + 0.5f) / tileSize) - 1;
        int maxX = Mathf.CeilToInt((centre.x + rx + 0.5f) / tileSize) + 1;
        int minZ = Mathf.FloorToInt((centre.z - rz + 0.5f) / tileSize) - 1;
        int maxZ = Mathf.CeilToInt((centre.z + rz + 0.5f) / tileSize) + 1;

        float GridX(int i) => i * tileSize - 0.5f;
        float GridZ(int i) => i * tileSize - 0.5f;

        // Distance from the centre as a fraction of the radius (1 = on the edge)
        float Reach(float x, float z)
        {
            float dx = (x - centre.x) / rx, dz = (z - centre.z) / rz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<int> triangles = new();
        Dictionary<(int, int), int> shared = new();

        // Same UV rule as Bitgem's builder: local position plus world position
        Vector3 origin = t.position;

        int Vertex(int i, int j)
        {
            if (shared.TryGetValue((i, j), out int index)) return index;

            float x = GridX(i), z = GridZ(j);

            // Corners poking past the circle are pulled back onto it, so the
            // edge is a smooth ring rather than a staircase of tiles
            float reach = Reach(x, z);
            if (reach > 1f)
            {
                x = centre.x + (x - centre.x) / reach;
                z = centre.z + (z - centre.z) / reach;
            }

            index = vertices.Count;
            vertices.Add(new Vector3(x, top, z));
            uvs.Add(new Vector2(x + origin.x, z + origin.z));
            shared[(i, j)] = index;
            return index;
        }

        for (int i = minX; i < maxX; i++)
        {
            for (int j = minZ; j < maxZ; j++)
            {
                // A tile is kept if its middle is inside the circle
                float midX = GridX(i) + tileSize * 0.5f;
                float midZ = GridZ(j) + tileSize * 0.5f;
                if (Reach(midX, midZ) > 1f) continue;

                int a = Vertex(i, j), b = Vertex(i, j + 1), c = Vertex(i + 1, j + 1), d = Vertex(i + 1, j);

                // Bitgem's winding: faces up
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(d); triangles.Add(a);
            }
        }

        Vector3[] normals = new Vector3[vertices.Count];
        Color[] colours = new Color[vertices.Count];
        for (int n = 0; n < vertices.Count; n++)
        {
            normals[n] = Vector3.up;
            colours[n] = Color.black;   // no foam line
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshPath));

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool created = mesh == null;
        if (created) mesh = new Mesh();

        mesh.Clear();
        mesh.name = "RoundSea";
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colours);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        // A little height in the bounds, so the vertex waves are not culled
        Bounds bounds = mesh.bounds;
        bounds.Expand(new Vector3(0f, 4f, 0f));
        mesh.bounds = bounds;

        if (created) AssetDatabase.CreateAsset(mesh, MeshPath);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(water, "Make Sea Round");
        water.sharedMesh = mesh;

        // Otherwise it rebuilds the square on its first frame
        foreach (WaterVolumeBase volume in water.GetComponents<WaterVolumeBase>())
        {
            Undo.RecordObject(volume, "Make Sea Round");
            volume.enabled = false;
        }

        EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
        Selection.activeGameObject = water.gameObject;

        Debug.Log($"Sea is now a {radius:0} m disc on a {tileSize} grid ({triangles.Count / 3} triangles), saved at {MeshPath}. " +
                  "The Bitgem water volume was switched off - tick it back on to return to the square.", water);
    }
}

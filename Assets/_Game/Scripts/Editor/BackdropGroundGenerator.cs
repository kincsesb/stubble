#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class BackdropGroundGenerator : EditorWindow
{
    const string POLYART = "Assets/Polyart/PolyartStudio/DreamscapeVillage/Prefabs/";

    // ── World layout ──────────────────────────────────────────────────────────
    Vector3 worldCenter    = new Vector3(100f, 0f, 100f);
    float   groundY        = -0.3f;

    // ── Terrain ───────────────────────────────────────────────────────────────
    float terrainSize         = 1600f;
    float terrainMaxHeight    = 50f;
    int   heightmapResolution = 513;   // must be 2^n + 1
    int   alphamapResolution  = 512;

    // ── Height shaping ────────────────────────────────────────────────────────
    float flatRadius         = 120f;
    float maxAmplitude       = 14f;
    float amplitudeRampStart = 200f;
    float amplitudeRampEnd   = 500f;
    int   perlinSeed         = 42;

    // ── Terrain layers (drag-and-drop in Inspector) ───────────────────────────
    TerrainLayer grassTerrainLayer;
    TerrainLayer dirtTerrainLayer;

    // ── Dirt ring (splatmap zone) ─────────────────────────────────────────────
    float dirtRingInner = 102f;
    float dirtRingOuter = 190f;

    // ── Fence perimeter ───────────────────────────────────────────────────────
    float fenceRadius = 100f;

    // ── Village cluster ───────────────────────────────────────────────────────
    float villageAngleDeg = 35f;
    float villageDist     = 290f;

    Vector2 _scroll;

    [MenuItem("Fields/Backdrop/Open Generator")]
    public static void ShowWindow() => GetWindow<BackdropGroundGenerator>("Backdrop Generator");

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("FIELDS — Backdrop Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("World Layout", EditorStyles.boldLabel);
        worldCenter = EditorGUILayout.Vector3Field("World Centre",  worldCenter);
        groundY     = EditorGUILayout.FloatField ("Ground Y",       groundY);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Unity Terrain", EditorStyles.boldLabel);
        terrainSize         = EditorGUILayout.FloatField("Terrain Size (m)",          terrainSize);
        terrainMaxHeight    = EditorGUILayout.FloatField("Terrain Max Height (m)",    terrainMaxHeight);
        heightmapResolution = EditorGUILayout.IntField  ("Heightmap Resolution",      heightmapResolution);
        alphamapResolution  = EditorGUILayout.IntField  ("Alphamap Resolution",       alphamapResolution);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Height Shaping (pre-sculpt base)", EditorStyles.boldLabel);
        flatRadius         = EditorGUILayout.FloatField("Flat Inner Radius",    flatRadius);
        maxAmplitude       = EditorGUILayout.FloatField("Max Amplitude (m)",    maxAmplitude);
        amplitudeRampStart = EditorGUILayout.FloatField("Amplitude Ramp Start", amplitudeRampStart);
        amplitudeRampEnd   = EditorGUILayout.FloatField("Amplitude Ramp End",   amplitudeRampEnd);
        perlinSeed         = EditorGUILayout.IntField  ("Perlin Seed",          perlinSeed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Terrain Layers (optional)", EditorStyles.boldLabel);
        grassTerrainLayer = (TerrainLayer)EditorGUILayout.ObjectField(
            "Grass Layer", grassTerrainLayer, typeof(TerrainLayer), false);
        dirtTerrainLayer  = (TerrainLayer)EditorGUILayout.ObjectField(
            "Dirt Layer",  dirtTerrainLayer,  typeof(TerrainLayer), false);
        dirtRingInner = EditorGUILayout.FloatField("Dirt Ring Inner (m)", dirtRingInner);
        dirtRingOuter = EditorGUILayout.FloatField("Dirt Ring Outer (m)", dirtRingOuter);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Fence & Village", EditorStyles.boldLabel);
        fenceRadius    = EditorGUILayout.FloatField("Fence Radius (m)",    fenceRadius);
        villageAngleDeg= EditorGUILayout.FloatField("Village Angle (deg)", villageAngleDeg);
        villageDist    = EditorGUILayout.FloatField("Village Distance (m)",villageDist);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Generate Backdrop", GUILayout.Height(36)))
            Generate();
        if (GUILayout.Button("Remove Backdrop",   GUILayout.Height(24)))
            RemoveExisting();

        EditorGUILayout.HelpBox(
            "After generating, sculpt & paint the terrain with Unity's Terrain Tools.\n" +
            "Re-generating will REPLACE the TerrainData — export a backup first if needed.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GENERATE
    // ═════════════════════════════════════════════════════════════════════════

    public void Generate()
    {
        Undo.SetCurrentGroupName("Generate Backdrop");
        int group = Undo.GetCurrentGroup();

        RemoveExisting();
        EnsureBackdropLayer();
        EnsureFolders();

        var root = new GameObject("--- BACKDROP ---");
        Undo.RegisterCreatedObjectUndo(root, "Backdrop Root");

        // 1. Unity Terrain (replaces procedural mesh; sculpt/paint freely after)
        BuildTerrain().transform.SetParent(root.transform, true);

        // 2. Distant terrain hills (Polyart DistantTerrain prefabs)
        PlaceDistantTerrain(root);

        // 3. Perimeter fence (Polyart VillageFence pieces)
        PlaceFencePerimeter(root);

        // 4. Tree clusters (Polyart Tree prefabs)
        PlaceTreeClusters(root);

        // 5. Village / farm buildings
        PlaceVillage(root);

        SetLayerRecursive(root, LayerMask.NameToLayer("Backdrop"));

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[Backdrop] Generation complete. Sculpt the terrain freely in the Scene view.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TERRAIN  — real Unity Terrain with Perlin base + optional layers
    // ─────────────────────────────────────────────────────────────────────────

    GameObject BuildTerrain()
    {
        const string tdPath = "Assets/_Game/Backdrop/TerrainData/BackdropTerrain.asset";

        // Always recreate TerrainData so resolution changes take effect cleanly
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
            AssetDatabase.DeleteAsset(tdPath);

        var td = new TerrainData();
        AssetDatabase.CreateAsset(td, tdPath);

        td.heightmapResolution = Mathf.Max(33, heightmapResolution);
        td.size = new Vector3(terrainSize, terrainMaxHeight, terrainSize);

        // ── Heights ───────────────────────────────────────────────────────────
        int res = td.heightmapResolution;
        var heights = new float[res, res];
        float pSeed = perlinSeed * 17.3f;

        for (int iz = 0; iz < res; iz++)
        for (int ix = 0; ix < res; ix++)
        {
            float lx = ((float)ix / (res - 1) - 0.5f) * terrainSize;
            float lz = ((float)iz / (res - 1) - 0.5f) * terrainSize;
            float wx = lx + worldCenter.x;
            float wz = lz + worldCenter.z;

            float dist = Mathf.Sqrt(lx * lx + lz * lz);
            float amp  = 0f;
            if (dist > flatRadius)
            {
                float t = Mathf.Clamp01((dist - amplitudeRampStart) / (amplitudeRampEnd - amplitudeRampStart));
                amp = maxAmplitude * t * t;
            }

            float n = Mathf.PerlinNoise((wx + pSeed) * 0.0035f, (wz + pSeed) * 0.0035f)
                    + Mathf.PerlinNoise((wx + pSeed) * 0.008f,  (wz + pSeed) * 0.008f) * 0.4f
                    + Mathf.PerlinNoise((wx + pSeed) * 0.02f,   (wz + pSeed) * 0.02f)  * 0.15f;

            // Unity heightmap: [z-row, x-column], normalized 0-1
            heights[iz, ix] = Mathf.Clamp01((n * amp) / terrainMaxHeight);
        }

        td.SetHeights(0, 0, heights);

        // ── Terrain Layers & Splatmap ─────────────────────────────────────────
        bool hasGrass = grassTerrainLayer != null;
        bool hasDirt  = dirtTerrainLayer  != null;

        if (hasGrass)
        {
            td.terrainLayers = hasDirt
                ? new[] { grassTerrainLayer, dirtTerrainLayer }
                : new[] { grassTerrainLayer };

            if (hasDirt)
            {
                td.alphamapResolution = Mathf.Max(16, alphamapResolution);
                int ares = td.alphamapResolution;
                var splat = new float[ares, ares, 2];

                for (int iz = 0; iz < ares; iz++)
                for (int ix = 0; ix < ares; ix++)
                {
                    float lx = ((float)ix / (ares - 1) - 0.5f) * terrainSize;
                    float lz = ((float)iz / (ares - 1) - 0.5f) * terrainSize;
                    float dist = Mathf.Sqrt(lx * lx + lz * lz);

                    float dirt = 0f;
                    if (dist >= dirtRingInner && dist <= dirtRingOuter)
                    {
                        float halfW  = (dirtRingOuter - dirtRingInner) * 0.5f;
                        float center = (dirtRingInner + dirtRingOuter) * 0.5f;
                        dirt = Mathf.SmoothStep(0f, 1f,
                            1f - Mathf.Clamp01(Mathf.Abs(dist - center) / halfW));
                    }

                    splat[iz, ix, 0] = 1f - dirt;
                    splat[iz, ix, 1] = dirt;
                }

                td.SetAlphamaps(0, 0, splat);
            }
        }

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        // ── GameObject ────────────────────────────────────────────────────────
        // Terrain origin is bottom-left corner, so offset by half-size
        float tx = worldCenter.x - terrainSize * 0.5f;
        float tz = worldCenter.z - terrainSize * 0.5f;

        var terrainGO = Terrain.CreateTerrainGameObject(td);
        Undo.RegisterCreatedObjectUndo(terrainGO, "Backdrop Terrain");
        terrainGO.name = "Backdrop_Terrain";
        terrainGO.transform.position = new Vector3(tx, groundY, tz);

        var terrain = terrainGO.GetComponent<Terrain>();
        terrain.shadowCastingMode = ShadowCastingMode.Off;
        terrain.drawInstanced     = true;

        return terrainGO;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DISTANT TERRAIN  — Polyart DistantTerrain prefabs as background hills
    // ─────────────────────────────────────────────────────────────────────────

    void PlaceDistantTerrain(GameObject root)
    {
        string[] variants =
        {
            POLYART + "Terrain/Terrain_DistantTerrain_01.prefab",
            POLYART + "Terrain/Terrain_DistantTerrain_02.prefab",
            POLYART + "Terrain/Terrain_DistantTerrain_03.prefab",
        };

        var parent = new GameObject("Backdrop_DistantTerrain");
        parent.transform.SetParent(root.transform, true);

        var rng   = new System.Random(perlinSeed + 99);
        int count = 8;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            float dist  = 560f + (float)(rng.NextDouble() * 130f);
            float x     = worldCenter.x + Mathf.Sin(angle) * dist;
            float z     = worldCenter.z + Mathf.Cos(angle) * dist;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variants[i % variants.Length]);
            if (prefab == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "DistantTerrain");
            go.name = $"DistantTerrain_{i}";
            go.transform.position = new Vector3(x, groundY - 8f, z);
            float yaw = angle * Mathf.Rad2Deg + (float)(rng.NextDouble() * 60f - 30f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float s = 0.85f + (float)(rng.NextDouble() * 0.35f);
            go.transform.localScale = new Vector3(s, s * 0.55f, s);

            ApplyBackdropSettings(go);
            SetupStatic(go);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FENCE PERIMETER
    // ─────────────────────────────────────────────────────────────────────────

    void PlaceFencePerimeter(GameObject root)
    {
        string path   = POLYART + "Props/Exterior/Props_VillageFence.prefab";
        var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[Backdrop] VillageFence prefab not found."); return; }

        var parent = new GameObject("Backdrop_Fence");
        parent.transform.SetParent(root.transform, true);
        SetupStatic(parent);

        float r   = fenceRadius;
        float len = 3.31f;
        float cx  = worldCenter.x, cz = worldCenter.z;

        TileFenceLine(prefab, parent, new Vector3(cx - r, groundY, cz + r), new Vector3(cx + r, groundY, cz + r), len, 90f);
        TileFenceLine(prefab, parent, new Vector3(cx + r, groundY, cz - r), new Vector3(cx - r, groundY, cz - r), len, 90f);
        TileFenceLine(prefab, parent, new Vector3(cx + r, groundY, cz - r), new Vector3(cx + r, groundY, cz + r), len, 0f);
        TileFenceLine(prefab, parent, new Vector3(cx - r, groundY, cz + r), new Vector3(cx - r, groundY, cz - r), len, 0f);
    }

    void TileFenceLine(GameObject prefab, GameObject parent,
                       Vector3 start, Vector3 end, float pieceLen, float yRot)
    {
        Vector3 dir   = (end - start).normalized;
        float   total = Vector3.Distance(start, end);
        int     count = Mathf.FloorToInt(total / pieceLen);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + dir * (i * pieceLen + pieceLen * 0.5f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "Fence");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            ApplyBackdropSettings(go);
            SetupStatic(go);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TREE CLUSTERS
    // ─────────────────────────────────────────────────────────────────────────

    void PlaceTreeClusters(GameObject root)
    {
        string[] treePaths =
        {
            POLYART + "Trees/Tree_Crown_01.prefab",
            POLYART + "Trees/Tree_Crown_02.prefab",
            POLYART + "Trees/Tree_Crown_03.prefab",
            POLYART + "Trees/Tree_Village_Oak.prefab",
            POLYART + "Trees/Combined/BigTree.prefab",
        };

        var parent = new GameObject("Backdrop_Trees");
        parent.transform.SetParent(root.transform, true);

        var rng = new System.Random(perlinSeed + 1);

        for (int ci = 0; ci < 40; ci++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist  = 190f + (float)(rng.NextDouble() * 290f);
            float bx = worldCenter.x + Mathf.Sin(angle) * dist;
            float bz = worldCenter.z + Mathf.Cos(angle) * dist;

            int treesInClump = dist < 300f ? (2 + rng.Next(0, 4)) : (1 + rng.Next(0, 3));

            for (int ti = 0; ti < treesInClump; ti++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * 22f;
                float oz = (float)(rng.NextDouble() - 0.5) * 22f;

                int maxIdx = dist > 380f ? 3 : (dist > 280f ? 4 : 5);
                string treePath = treePaths[rng.Next(0, maxIdx)];

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                Undo.RegisterCreatedObjectUndo(go, "Tree");
                go.transform.position = new Vector3(bx + ox, groundY, bz + oz);
                go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f);
                float s = 0.75f + (float)(rng.NextDouble() * 0.55f);
                go.transform.localScale = Vector3.one * s;

                ApplyBackdropSettings(go);
                SetupStatic(go);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VILLAGE
    // ─────────────────────────────────────────────────────────────────────────

    void PlaceVillage(GameObject root)
    {
        var parent = new GameObject("Backdrop_Village");
        parent.transform.SetParent(root.transform, true);

        float rad = villageAngleDeg * Mathf.Deg2Rad;
        float vx  = worldCenter.x + Mathf.Sin(rad) * villageDist;
        float vz  = worldCenter.z + Mathf.Cos(rad) * villageDist;

        var buildings = new (string p, float ox, float oz, float yr)[]
        {
            (POLYART + "Modular/Full Houses/No Interiors/House_Tower.prefab",                         0f,    0f,  15f),
            (POLYART + "Modular/Full Houses/No Interiors/Merged/House_2x2_01_Merged.prefab",         20f,   -4f,  40f),
            (POLYART + "Modular/Full Houses/No Interiors/Merged/House_2x2_02_Merged.prefab",         14f,   16f, -25f),
            (POLYART + "Modular/Full Houses/No Interiors/Merged/House_2x1_01_Merged.prefab",        -16f,    6f, -10f),
            (POLYART + "Modular/Full Houses/No Interiors/Merged/House_2x1_Tall_01_Merged.prefab",   -22f,   -6f,  30f),
            (POLYART + "Structures/Structure_Barn.prefab",                                            -6f,  -18f,  90f),
            (POLYART + "Structures/Structure_WindmillBuilding.prefab",                                30f,   18f,  10f),
        };

        foreach (var b in buildings)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(b.p);
            if (prefab == null) { Debug.LogWarning("[Backdrop] Not found: " + b.p); continue; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "Village");
            go.transform.position = new Vector3(vx + b.ox, groundY, vz + b.oz);
            go.transform.rotation = Quaternion.Euler(0f, b.yr, 0f);
            ApplyBackdropSettings(go);
            SetupStatic(go);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    void ApplyBackdropSettings(GameObject go)
    {
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.shadowCastingMode          = ShadowCastingMode.Off;
            mr.receiveShadows             = false;
            mr.lightProbeUsage            = LightProbeUsage.Off;
            mr.reflectionProbeUsage       = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    static void SetupStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Backdrop"))
            AssetDatabase.CreateFolder("Assets/_Game", "Backdrop");
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Backdrop/TerrainData"))
            AssetDatabase.CreateFolder("Assets/_Game/Backdrop", "TerrainData");
    }

    static void EnsureBackdropLayer()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        bool found = false;
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == "Backdrop") { found = true; break; }
        }
        if (!found)
        {
            for (int i = 8; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(e.stringValue))
                {
                    e.stringValue = "Backdrop";
                    tagManager.ApplyModifiedProperties();
                    break;
                }
            }
        }
    }

    void RemoveExisting()
    {
        var existing = GameObject.Find("--- BACKDROP ---");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[Backdrop] Removed existing backdrop.");
        }
    }
}
#endif

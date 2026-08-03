using System.Collections.Generic;
using Fields.Core.Data;
using UnityEngine;

namespace Fields.Grass
{
    /// <summary>
    /// Generates and manages 10×10m grass mesh chunks for a single GrassField.
    /// Each chunk is one combined mesh (1 draw call). Shares the field's RenderTexture mask.
    /// LOD: 3 levels — Full / 50% / 20% density by distance.
    /// </summary>
    [RequireComponent(typeof(GrassField))]
    public class GrassChunkManager : MonoBehaviour
    {
        [Header("Config")]
        public GameConfig config;
        public Material grassMaterial;

        [Header("Chunk settings")]
        public float chunkSize = 10f;
        [Tooltip("Grass blades per metre² at full density")]
        public float bladeDensity = 180f;

        [Header("LOD distances")]
        public float lod1Distance = 20f;
        public float lod2Distance = 45f;

        [Header("Terrain Layer Exclusion")]
        [Tooltip("Layer indices where grass will NOT spawn (e.g. 1 = Village_Dirt)")]
        public int[] excludedLayerIndices = new int[] { 1 };
        [Tooltip("Minimum layer weight (0-1) to suppress grass")]
        [Range(0.01f, 1f)] public float excludeThreshold = 0.5f;

        GrassField _grassField;
        List<GrassChunk> _chunks = new List<GrassChunk>();
        Material _matInstance; // per-terrain instance so each field binds its own RT

        float[,,] _alphamap;
        int _alphamapW, _alphamapH, _alphamapLayers;

        Transform _camera;
        int _lodUpdateFrame;
        const int LOD_UPDATE_INTERVAL = 8; // check every N frames

        // ------------------------------------------------------------------ //

        void Awake()
        {
            _grassField = GetComponent<GrassField>();
        }

        void OnDestroy()
        {
            if (_matInstance != null) Destroy(_matInstance);
            foreach (var c in _chunks)
                if (c.lodMeshes != null)
                    foreach (var m in c.lodMeshes) if (m != null) Destroy(m);
        }

        void Start()
        {
            GenerateChunks();
            _camera = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            // Stagger LOD updates across frames to avoid per-frame mesh swaps
            if (++_lodUpdateFrame % LOD_UPDATE_INTERVAL == 0)
                UpdateChunkLODs();
        }

        // ------------------------------------------------------------------ //

        void GenerateChunks()
        {
            foreach (var c in _chunks) if (c.go != null) Destroy(c.go);
            _chunks.Clear();

            // Cache terrain splatmap for layer-based exclusion (CPU-side, one-time at Start)
            _alphamap = null;
            if (excludedLayerIndices != null && excludedLayerIndices.Length > 0)
            {
                var terrain = GetComponent<Terrain>();
                if (terrain != null)
                {
                    var td = terrain.terrainData;
                    _alphamapW = td.alphamapWidth;
                    _alphamapH = td.alphamapHeight;
                    _alphamapLayers = td.alphamapLayers;
                    _alphamap = td.GetAlphamaps(0, 0, _alphamapW, _alphamapH);
                }
            }

            // Create one material instance per terrain with the RT already bound,
            // so all chunks inherit the correct mask without auto-copying the source material.
            if (_matInstance != null) Destroy(_matInstance);
            _matInstance = new Material(grassMaterial);
            if (_grassField.MaskRenderTexture != null)
                _matInstance.SetTexture("_GrassMask", _grassField.MaskRenderTexture);

            int cols = Mathf.CeilToInt(_grassField.fieldSize.x / chunkSize);
            int rows = Mathf.CeilToInt(_grassField.fieldSize.y / chunkSize);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    _chunks.Add(BuildChunk(c, r));
        }

        GrassChunk BuildChunk(int col, int row)
        {
            // Field origin = GO's local (0,0) — bottom-left, matching Unity Terrain convention.
            float ox = col * chunkSize;
            float oz = row * chunkSize;
            Vector3 origin = transform.TransformPoint(new Vector3(ox, 0f, oz));

            var go = new GameObject($"GrassChunk_{col}_{row}");
            go.transform.SetParent(transform);
            go.transform.position = origin;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _matInstance; // share per-terrain instance, not the source asset
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Pre-build all 3 LOD meshes once; swap refs at runtime (zero GC)
            var lodMeshes = new Mesh[3];
            lodMeshes[0] = BuildBladeMesh(chunkSize, chunkSize, 1.0f, ox, oz);
            lodMeshes[1] = BuildBladeMesh(chunkSize, chunkSize, 0.5f, ox, oz);
            lodMeshes[2] = BuildBladeMesh(chunkSize, chunkSize, 0.2f, ox, oz);
            mf.sharedMesh = lodMeshes[0];

            return new GrassChunk { go = go, mr = mr, mf = mf, col = col, row = row,
                                    originX = ox, originZ = oz, lodMeshes = lodMeshes };
        }

        /// <summary>
        /// Generates a flat quad mesh for grass blades at given density fraction.
        /// Each "blade" is a vertical quad; final geometry is shaped in the vertex shader.
        /// originX/Z = chunk's local offset within the field (0-based, bottom-left convention).
        /// </summary>
        Mesh BuildBladeMesh(float width, float depth, float densityFraction, float originX = 0f, float originZ = 0f)
        {
            int maxBlades = Mathf.RoundToInt(width * depth * bladeDensity * densityFraction);
            maxBlades = Mathf.Min(maxBlades, 30000);

            var verts    = new Vector3[maxBlades * 4];
            var uvs      = new Vector2[maxBlades * 4];
            var tris     = new int[maxBlades * 6];
            var fieldUVs = new Vector2[maxBlades * 4];

            const float bladeHalfWidth = 0.16f;
            const float bladeHeight    = 0.80f;

            // Try up to 4× more positions to fill target density despite exclusion zones
            int attempts = _alphamap != null ? maxBlades * 4 : maxBlades;
            int placed = 0;

            for (int i = 0; i < attempts && placed < maxBlades; i++)
            {
                float rx = Random.Range(0f, width);
                float rz = Random.Range(0f, depth);
                float maskU = (originX + rx) / _grassField.fieldSize.x;
                float maskV = (originZ + rz) / _grassField.fieldSize.y;

                if (_alphamap != null && IsOnExcludedLayer(maskU, maskV)) continue;

                int v = placed * 4;
                verts[v + 0] = new Vector3(rx - bladeHalfWidth, 0f,         rz);
                verts[v + 1] = new Vector3(rx + bladeHalfWidth, 0f,         rz);
                verts[v + 2] = new Vector3(rx - bladeHalfWidth, bladeHeight, rz);
                verts[v + 3] = new Vector3(rx + bladeHalfWidth, bladeHeight, rz);

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);

                var fuv = new Vector2(maskU, maskV);
                fieldUVs[v + 0] = fieldUVs[v + 1] = fieldUVs[v + 2] = fieldUVs[v + 3] = fuv;

                int t = placed * 6;
                tris[t + 0] = v;     tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
                placed++;
            }

            // Trim arrays to actual placed count
            System.Array.Resize(ref verts,    placed * 4);
            System.Array.Resize(ref uvs,      placed * 4);
            System.Array.Resize(ref fieldUVs, placed * 4);
            System.Array.Resize(ref tris,     placed * 6);

            var mesh = new Mesh
            {
                name        = "GrassBladeMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, fieldUVs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        bool IsOnExcludedLayer(float u, float v)
        {
            int ax = Mathf.Clamp(Mathf.FloorToInt(u * _alphamapW), 0, _alphamapW - 1);
            // Unity alphamap: first index = Z (depth), second = X (width)
            int ay = Mathf.Clamp(Mathf.FloorToInt(v * _alphamapH), 0, _alphamapH - 1);
            foreach (int li in excludedLayerIndices)
                if (li < _alphamapLayers && _alphamap[ay, ax, li] > excludeThreshold)
                    return true;
            return false;
        }

        void UpdateChunkLODs()
        {
            if (_camera == null) return;
            Vector3 camPos = _camera.position;

            foreach (var chunk in _chunks)
            {
                if (chunk.go == null) continue;
                float sqDist = (camPos - chunk.go.transform.position).sqrMagnitude;

                float d1 = lod1Distance, d2 = lod2Distance;
                int band = sqDist < d1 * d1 ? 0 : sqDist < d2 * d2 ? 1 : 2;
                if (band != chunk.lastLODBand)
                {
                    chunk.lastLODBand = band;
                    chunk.mf.sharedMesh = chunk.lodMeshes[band]; // zero GC — just swap ref
                }
            }
        }

        // ------------------------------------------------------------------ //

        class GrassChunk
        {
            public GameObject go;
            public MeshRenderer mr;
            public MeshFilter mf;
            public int col, row;
            public float originX, originZ;
            public int lastLODBand = -1;
            public Mesh[] lodMeshes; // pre-built, swapped at runtime
        }
    }
}

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
        public float bladeDensity = 48f;

        [Header("LOD distances")]
        public float lod1Distance = 15f;
        public float lod2Distance = 30f;

        GrassField _grassField;
        List<GrassChunk> _chunks = new List<GrassChunk>();
        Material _matInstance; // per-terrain instance so each field binds its own RT

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
            int totalBlades = Mathf.RoundToInt(width * depth * bladeDensity * densityFraction);
            totalBlades = Mathf.Min(totalBlades, 65000 / 4); // stay under 16-bit index limit

            var verts = new Vector3[totalBlades * 4];
            var uvs = new Vector2[totalBlades * 4];
            var tris = new int[totalBlades * 6];
            var fieldUVs = new Vector2[totalBlades * 4]; // passed to shader for mask sampling

            for (int i = 0; i < totalBlades; i++)
            {
                float rx = Random.Range(0f, width);
                float rz = Random.Range(0f, depth);
                // UV into the full field mask RT (0-based bottom-left)
                float maskU = (originX + rx) / _grassField.fieldSize.x;
                float maskV = (originZ + rz) / _grassField.fieldSize.y;

                int v = i * 4;
                float hw = 0.04f;
                verts[v + 0] = new Vector3(rx - hw, 0f, rz);
                verts[v + 1] = new Vector3(rx + hw, 0f, rz);
                verts[v + 2] = new Vector3(rx - hw, 0.35f, rz);
                verts[v + 3] = new Vector3(rx + hw, 0.35f, rz);

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);

                // Store per-blade field UV in UV2 for mask sampling in shader
                var fuv = new Vector2(maskU, maskV);
                fieldUVs[v + 0] = fieldUVs[v + 1] = fieldUVs[v + 2] = fieldUVs[v + 3] = fuv;

                int t = i * 6;
                tris[t + 0] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "GrassBladeMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, fieldUVs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

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
        public float bladeDensity = 16f;

        [Header("LOD distances")]
        public float lod1Distance = 15f;
        public float lod2Distance = 30f;

        GrassField _grassField;
        List<GrassChunk> _chunks = new List<GrassChunk>();

        Transform _camera;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            _grassField = GetComponent<GrassField>();
        }

        void Start()
        {
            GenerateChunks();
            _camera = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            UpdateChunkLODs();
        }

        // ------------------------------------------------------------------ //

        void GenerateChunks()
        {
            foreach (var c in _chunks) if (c.go != null) Destroy(c.go);
            _chunks.Clear();

            int cols = Mathf.CeilToInt(_grassField.fieldSize.x / chunkSize);
            int rows = Mathf.CeilToInt(_grassField.fieldSize.y / chunkSize);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    _chunks.Add(BuildChunk(c, r));

            // Bind the shared RenderTexture mask to the material
            if (grassMaterial != null && _grassField.MaskRenderTexture != null)
                grassMaterial.SetTexture("_GrassMask", _grassField.MaskRenderTexture);
        }

        GrassChunk BuildChunk(int col, int row)
        {
            float ox = col * chunkSize - _grassField.fieldSize.x * 0.5f;
            float oz = row * chunkSize - _grassField.fieldSize.y * 0.5f;
            Vector3 origin = transform.TransformPoint(new Vector3(ox, 0f, oz));

            var go = new GameObject($"GrassChunk_{col}_{row}");
            go.transform.SetParent(transform);
            go.transform.position = origin;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = grassMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            mf.sharedMesh = BuildBladeMesh(chunkSize, chunkSize, 1f);

            return new GrassChunk { go = go, mr = mr, mf = mf, col = col, row = row };
        }

        /// <summary>
        /// Generates a flat quad mesh for grass blades at given density fraction.
        /// Each "blade" is a vertical quad; final geometry is shaped in the vertex shader.
        /// </summary>
        Mesh BuildBladeMesh(float width, float depth, float densityFraction)
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
                float maskU = (rx + _grassField.fieldSize.x * 0.5f - verts[0].x) / _grassField.fieldSize.x;
                float maskV = (rz + _grassField.fieldSize.y * 0.5f) / _grassField.fieldSize.y;

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
                float dist = Vector3.Distance(camPos, chunk.go.transform.position);

                float densityFraction = dist < lod1Distance ? 1f :
                                        dist < lod2Distance ? 0.5f : 0.2f;

                // Rebuild mesh only if LOD band changed
                int band = dist < lod1Distance ? 0 : dist < lod2Distance ? 1 : 2;
                if (band != chunk.lastLODBand)
                {
                    chunk.lastLODBand = band;
                    chunk.mf.sharedMesh = BuildBladeMesh(chunkSize, chunkSize, densityFraction);
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
            public int lastLODBand = -1;
        }
    }
}

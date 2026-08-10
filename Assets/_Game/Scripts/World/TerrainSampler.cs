using UnityEngine;

namespace Fields.World
{
    public struct GroundSample
    {
        public float   height;
        public Vector3 normal;
        /// <summary>0=uncut grass, 1=cut stubble, 2=dirt, 3=gravel</summary>
        public byte    surfaceIndex;
        public bool    valid;
    }

    /// <summary>
    /// Multi-terrain ground sampler. Alphamaps are cached once at startup — GetAlphamaps
    /// is never called per-frame. Normals are blended across a configurable band at terrain seams.
    /// </summary>
    public class TerrainSampler : MonoBehaviour
    {
        public static TerrainSampler Instance { get; private set; }

        [Header("Splatmap layer mapping")]
        [Tooltip("Terrain layer index that represents dirt/path")]
        public int dirtLayerIndex = 1;
        [Tooltip("Terrain layer index that represents gravel (−1 = none)")]
        public int gravelLayerIndex = -1;
        [Tooltip("Normal blend band width at terrain boundaries (m)")]
        public float boundaryBlendWidth = 2f;

        struct TerrainCache
        {
            public UnityEngine.Terrain terrain;
            public float minX, minZ, maxX, maxZ;
            public int alphamapW, alphamapH;
            public byte[,] dominantLayer; // [alphamapZ, alphamapX]
        }

        TerrainCache[] _caches;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildCache();
        }

        void BuildCache()
        {
            var terrains = UnityEngine.Terrain.activeTerrains;
            _caches = new TerrainCache[terrains.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                var td = t.terrainData;
                Vector3 pos  = t.transform.position;
                Vector3 size = td.size;
                int aw = td.alphamapWidth, ah = td.alphamapHeight;

                float[,,] raw = td.GetAlphamaps(0, 0, aw, ah); // only allocation
                int layers = raw.GetLength(2);
                var dominant = new byte[ah, aw];
                for (int az = 0; az < ah; az++)
                    for (int ax = 0; ax < aw; ax++)
                    {
                        int best = 0; float bestW = raw[az, ax, 0];
                        for (int l = 1; l < layers; l++)
                            if (raw[az, ax, l] > bestW) { bestW = raw[az, ax, l]; best = l; }
                        dominant[az, ax] = LayerToSurface(best);
                    }

                _caches[i] = new TerrainCache
                {
                    terrain = t,
                    minX = pos.x, minZ = pos.z,
                    maxX = pos.x + size.x, maxZ = pos.z + size.z,
                    alphamapW = aw, alphamapH = ah,
                    dominantLayer = dominant,
                };
            }
        }

        byte LayerToSurface(int idx)
        {
            if (idx == dirtLayerIndex)                         return 2;
            if (gravelLayerIndex >= 0 && idx == gravelLayerIndex) return 3;
            return 0;
        }

        // ------------------------------------------------------------------ //

        public GroundSample Sample(Vector3 worldPos)
        {
            if (_caches == null || _caches.Length == 0) return default;

            int primary = FindTerrain(worldPos);
            if (primary < 0) return default;

            var result = SampleOne(primary, worldPos);

            // Blend normals across terrain seams
            for (int i = 0; i < _caches.Length; i++)
            {
                if (i == primary) continue;
                ref var nc = ref _caches[i];
                float edgeDist = Mathf.Min(
                    Mathf.Min(Mathf.Abs(worldPos.x - nc.minX), Mathf.Abs(worldPos.x - nc.maxX)),
                    Mathf.Min(Mathf.Abs(worldPos.z - nc.minZ), Mathf.Abs(worldPos.z - nc.maxZ)));
                if (edgeDist >= boundaryBlendWidth) continue;

                float t = 1f - edgeDist / boundaryBlendWidth;
                var ns = SampleOne(i, worldPos);
                if (ns.valid)
                    result.normal = Vector3.Slerp(result.normal, ns.normal, t * 0.5f).normalized;
            }

            return result;
        }

        GroundSample SampleOne(int idx, Vector3 worldPos)
        {
            ref var c = ref _caches[idx];
            var t = c.terrain;

            float spanX = c.maxX - c.minX;
            float spanZ = c.maxZ - c.minZ;
            float u = Mathf.Clamp01((worldPos.x - c.minX) / spanX);
            float v = Mathf.Clamp01((worldPos.z - c.minZ) / spanZ);

            int ax = Mathf.Clamp(Mathf.FloorToInt(u * c.alphamapW), 0, c.alphamapW - 1);
            int az = Mathf.Clamp(Mathf.FloorToInt(v * c.alphamapH), 0, c.alphamapH - 1);

            return new GroundSample
            {
                height       = t.SampleHeight(worldPos),
                normal       = t.terrainData.GetInterpolatedNormal(u, v),
                surfaceIndex = c.dominantLayer[az, ax],
                valid        = true,
            };
        }

        int FindTerrain(Vector3 worldPos)
        {
            for (int i = 0; i < _caches.Length; i++)
            {
                ref var c = ref _caches[i];
                if (worldPos.x >= c.minX && worldPos.x <= c.maxX &&
                    worldPos.z >= c.minZ && worldPos.z <= c.maxZ) return i;
            }
            return -1;
        }
    }
}
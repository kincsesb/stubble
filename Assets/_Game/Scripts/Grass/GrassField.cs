using System;
using Fields.Core.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fields.Grass
{
    /// <summary>
    /// Dual-representation grass field per spec §4.
    /// GPU: R8 RenderTexture (visual); CPU: bool[,] grid (gameplay/save/sync).
    /// GPU mask is write-only from CPU — never read back.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        [Header("Config")]
        public GameConfig config;
        [Tooltip("World-space size of this field (X = width, Y = height)")]
        public Vector2 fieldSize = new Vector2(30f, 40f);
        [Tooltip("RT resolution: 1024 or 2048")]
        public int renderTextureSize = 1024;

        // GPU state
        RenderTexture _maskRT;
        RenderTexture _maskRTSwap;   // ping-pong buffer — no self-referencing blit
        Material _cutMaskMaterial;    // writes circles/capsules to the RT

        // CPU state — bool[col, row], true = uncut
        bool[,] _cutGrid;
        int _gridCols;
        int _gridRows;
        int _totalCells;
        int _cutCount;

        public event Action<int, int> OnCellCut; // (gridCol, gridRow)

        public RenderTexture MaskRenderTexture => _maskRT;

        /// <summary>
        /// Co-op hook: set by GrassNetSync on non-host clients to route cuts through the network.
        /// Returns true → cut was intercepted (local apply suppressed).
        /// Null → single-player / host, cut applies locally.
        /// </summary>
        public Func<Vector3, float, bool>         CutAreaInterceptor;
        public Func<Vector3, Vector3, float, bool> CutCapsuleInterceptor;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            InitCpuGrid();
            InitGpuBuffers();
        }

        void OnDestroy()
        {
            _maskRT?.Release();
            _maskRTSwap?.Release();
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>Cut a circle at world position with given radius.</summary>
        public void CutArea(Vector3 worldPos, float radius)
        {
            if (CutAreaInterceptor != null && CutAreaInterceptor(worldPos, radius)) return;
            // CPU
            WorldToGrid(worldPos, out int cx, out int cz);
            int cellRadius = Mathf.CeilToInt(radius / config.gridCellSize);
            int cellRadiusSq = cellRadius * cellRadius;

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    if (dx * dx + dz * dz > cellRadiusSq) continue;
                    int col = cx + dx;
                    int row = cz + dz;
                    if (!InGrid(col, row)) continue;
                    TryCutCell(col, row);
                }
            }

            // GPU
            Vector2 uv = WorldToUV(worldPos);
            float uvRadius = radius / fieldSize.x;
            DrawCircleToRT(uv, uvRadius);
        }

        /// <summary>Cut a capsule between two world positions.</summary>
        public void CutCapsule(Vector3 from, Vector3 to, float radius)
        {
            if (CutCapsuleInterceptor != null && CutCapsuleInterceptor(from, to, radius)) return;
            // CPU — rasterise the capsule onto the grid
            WorldToGrid(from, out int fx, out int fz);
            WorldToGrid(to, out int tx, out int tz);
            int cellRadius = Mathf.CeilToInt(radius / config.gridCellSize);

            int minCol = Mathf.Max(0, Mathf.Min(fx, tx) - cellRadius);
            int maxCol = Mathf.Min(_gridCols - 1, Mathf.Max(fx, tx) + cellRadius);
            int minRow = Mathf.Max(0, Mathf.Min(fz, tz) - cellRadius);
            int maxRow = Mathf.Min(_gridRows - 1, Mathf.Max(fz, tz) + cellRadius);

            float radiusSq = (radius / config.gridCellSize) * (radius / config.gridCellSize);
            Vector2 seg = new Vector2(tx - fx, tz - fz);
            float segLenSq = seg.sqrMagnitude;

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    float distSq = PointToSegmentSq(col - fx, row - fz, seg, segLenSq);
                    if (distSq <= radiusSq)
                        TryCutCell(col, row);
                }
            }

            // GPU
            Vector2 uvFrom = WorldToUV(from);
            Vector2 uvTo = WorldToUV(to);
            float uvRadius = radius / fieldSize.x;
            DrawCapsuleToRT(uvFrom, uvTo, uvRadius);
        }

        public float GetCompletionPercent()
        {
            if (_totalCells == 0) return 0f;
            return (float)_cutCount / _totalCells * 100f;
        }

        /// <summary>Returns a copy of the cut grid for saving. True = uncut.</summary>
        public bool[,] GetCutGrid() => (bool[,])_cutGrid.Clone();

        /// <summary>Restores cut state from a loaded save (true = uncut).</summary>
        public void LoadCutGrid(bool[,] grid)
        {
            _cutCount = 0;
            for (int row = 0; row < _gridRows; row++)
                for (int col = 0; col < _gridCols; col++)
                {
                    _cutGrid[col, row] = grid[col, row];
                    if (!grid[col, row]) _cutCount++;
                }
            RebuildGpuFromCpu();
        }

        public int GridCols => _gridCols;
        public int GridRows => _gridRows;

        /// <summary>Converts a grid cell to world-space centre position.</summary>
        public Vector3 CellToWorld(int col, int row) => GridToWorld(col, row);

        // ------------------------------------------------------------------ //
        // Internals
        // ------------------------------------------------------------------ //

        void InitCpuGrid()
        {
            _gridCols = Mathf.RoundToInt(fieldSize.x / config.gridCellSize);
            _gridRows = Mathf.RoundToInt(fieldSize.y / config.gridCellSize);
            _totalCells = _gridCols * _gridRows;
            _cutGrid = new bool[_gridCols, _gridRows]; // default false = uncut
            // Populate all cells as uncut (true)
            for (int row = 0; row < _gridRows; row++)
                for (int col = 0; col < _gridCols; col++)
                    _cutGrid[col, row] = true;
        }

        void InitGpuBuffers()
        {
            var desc = new RenderTextureDescriptor(renderTextureSize, renderTextureSize,
                RenderTextureFormat.R8, 0);
            _maskRT = new RenderTexture(desc) { name = $"{name}_GrassMask" };
            _maskRTSwap = new RenderTexture(desc) { name = $"{name}_GrassMaskSwap" };
            _maskRT.Create();
            _maskRTSwap.Create();

            // Fill with white (1 = uncut)
            var cmd = CommandBufferPool.Get("ClearGrassMask");
            cmd.SetRenderTarget(_maskRT);
            cmd.ClearRenderTarget(false, true, Color.white);
            cmd.SetRenderTarget(_maskRTSwap);
            cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void TryCutCell(int col, int row)
        {
            if (!_cutGrid[col, row]) return;
            _cutGrid[col, row] = false;
            _cutCount++;
            OnCellCut?.Invoke(col, row);
        }

        void DrawCircleToRT(Vector2 centerUV, float uvRadius)
        {
            if (_cutMaskMaterial == null)
                _cutMaskMaterial = new Material(Shader.Find("Hidden/Fields/GrassMaskWrite"));
            if (_cutMaskMaterial == null) return;

            _cutMaskMaterial.SetVector("_Center", new Vector4(centerUV.x, centerUV.y, 0, 0));
            _cutMaskMaterial.SetFloat("_Radius", uvRadius);
            _cutMaskMaterial.SetFloat("_IsCircle", 1f);

            var cmd = CommandBufferPool.Get("DrawGrassCut");
            cmd.Blit(_maskRT, _maskRTSwap, _cutMaskMaterial);
            cmd.Blit(_maskRTSwap, _maskRT);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void DrawCapsuleToRT(Vector2 uvFrom, Vector2 uvTo, float uvRadius)
        {
            if (_cutMaskMaterial == null)
                _cutMaskMaterial = new Material(Shader.Find("Hidden/Fields/GrassMaskWrite"));
            if (_cutMaskMaterial == null) return;

            _cutMaskMaterial.SetVector("_Center", new Vector4(uvFrom.x, uvFrom.y, uvTo.x, uvTo.y));
            _cutMaskMaterial.SetFloat("_Radius", uvRadius);
            _cutMaskMaterial.SetFloat("_IsCircle", 0f);

            var cmd = CommandBufferPool.Get("DrawGrassCapsule");
            cmd.Blit(_maskRT, _maskRTSwap, _cutMaskMaterial);
            cmd.Blit(_maskRTSwap, _maskRT);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void RebuildGpuFromCpu()
        {
            // Clear to white then draw black for every cut cell
            var cmd = CommandBufferPool.Get("RebuildGrassMask");
            cmd.SetRenderTarget(_maskRT);
            cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            for (int row = 0; row < _gridRows; row++)
                for (int col = 0; col < _gridCols; col++)
                    if (!_cutGrid[col, row])
                    {
                        Vector3 wp = GridToWorld(col, row);
                        float uvRadius = config.gridCellSize / fieldSize.x * 0.6f;
                        DrawCircleToRT(WorldToUV(wp), uvRadius);
                    }
        }

        // ------------------------------------------------------------------ //
        // Coordinate helpers
        // ------------------------------------------------------------------ //

        void WorldToGrid(Vector3 worldPos, out int col, out int row)
        {
            // Field origin = GO's local (0,0) — matches Unity Terrain's bottom-left convention.
            Vector3 local = transform.InverseTransformPoint(worldPos);
            col = Mathf.RoundToInt(local.x / config.gridCellSize);
            row = Mathf.RoundToInt(local.z / config.gridCellSize);
        }

        Vector2 WorldToUV(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            float u = local.x / fieldSize.x;
            float v = local.z / fieldSize.y;
            return new Vector2(u, v);
        }

        Vector3 GridToWorld(int col, int row)
        {
            float lx = col * config.gridCellSize;
            float lz = row * config.gridCellSize;
            return transform.TransformPoint(new Vector3(lx, 0f, lz));
        }

        /// <summary>Returns true if the world position lies within this field's bounds.</summary>
        public bool ContainsWorldPoint(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return local.x >= 0f && local.x <= fieldSize.x
                && local.z >= 0f && local.z <= fieldSize.y;
        }

        bool InGrid(int col, int row) =>
            col >= 0 && col < _gridCols && row >= 0 && row < _gridRows;

        static float PointToSegmentSq(float px, float py, Vector2 seg, float segLenSq)
        {
            if (segLenSq < 0.0001f) return px * px + py * py;
            float t = Mathf.Clamp01((px * seg.x + py * seg.y) / segLenSq);
            float closestX = t * seg.x;
            float closestY = t * seg.y;
            float dx = px - closestX;
            float dy = py - closestY;
            return dx * dx + dy * dy;
        }

        // ------------------------------------------------------------------ //
        // Debug Gizmos
        // ------------------------------------------------------------------ //

        void OnDrawGizmosSelected()
        {
            if (config == null) return;
            Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            // Field starts at local (0,0) — draw cube centred at half-size
            Gizmos.DrawWireCube(
                new Vector3(fieldSize.x * 0.5f, 0f, fieldSize.y * 0.5f),
                new Vector3(fieldSize.x, 0.1f, fieldSize.y));

            if (_cutGrid == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
            float cs = config.gridCellSize;
            for (int row = 0; row < _gridRows; row++)
                for (int col = 0; col < _gridCols; col++)
                    if (!_cutGrid[col, row])
                    {
                        float lx = col * cs;
                        float lz = row * cs;
                        Gizmos.DrawCube(new Vector3(lx, 0f, lz), new Vector3(cs, 0.05f, cs));
                    }
        }
    }
}
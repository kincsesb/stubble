using System;
using Fields.Core.Data;
using Fields.Grass;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Tracks loose hay per 6×6m collection cell.
    /// When a cell reaches the threshold, a HayPile is spawned and the cell resets.
    /// Leftover hay (below threshold) persists after parcel completion.
    /// </summary>
    [RequireComponent(typeof(GrassField))]
    public class HayAccumulationSystem : MonoBehaviour
    {
        [Header("References")]
        public GameConfig config;
        public GameObject hayPilePrefab;

        [Header("Loose Hay Decal Thresholds (0–1 of collection cell max)")]
        [Tooltip("Fraction at which decal advances to phase 2")]
        public float decalPhase2Threshold = 0.33f;
        [Tooltip("Fraction at which decal advances to phase 3")]
        public float decalPhase3Threshold = 0.66f;

        GrassField _grassField;

        // [collectionCol, collectionRow] = accumulated cut-cell count
        float[,] _hayGrid;
        int _collCols;
        int _collRows;

        // Decal GameObjects per cell (3 phases, only one active)
        GameObject[,] _decalObjects;

        public event Action<Vector3, float> OnHayPileSpawned; // worldPos, leftover

        void Awake()
        {
            _grassField = GetComponent<GrassField>();
            _grassField.OnCellCut += OnGrassCellCut;
        }

        void Start()
        {
            InitGrid();
        }

        void OnDestroy()
        {
            if (_grassField != null) _grassField.OnCellCut -= OnGrassCellCut;
        }

        // ------------------------------------------------------------------ //

        void InitGrid()
        {
            float fw = _grassField.fieldSize.x;
            float fh = _grassField.fieldSize.y;
            _collCols = Mathf.CeilToInt(fw / config.collectionCellSize);
            _collRows = Mathf.CeilToInt(fh / config.collectionCellSize);
            _hayGrid = new float[_collCols, _collRows];
            _decalObjects = new GameObject[_collCols, _collRows];
        }

        void OnGrassCellCut(int gridCol, int gridRow)
        {
            // Map grass grid cell → collection cell
            float cellWorldX = gridCol * config.gridCellSize;
            float cellWorldZ = gridRow * config.gridCellSize;
            int cc = Mathf.FloorToInt(cellWorldX / config.collectionCellSize);
            int cr = Mathf.FloorToInt(cellWorldZ / config.collectionCellSize);
            cc = Mathf.Clamp(cc, 0, _collCols - 1);
            cr = Mathf.Clamp(cr, 0, _collRows - 1);

            _hayGrid[cc, cr] += 1f;
            UpdateDecal(cc, cr);

            if (_hayGrid[cc, cr] >= config.hayUnitsPerCollectionCell)
            {
                SpawnHayPile(cc, cr);
            }
        }

        void SpawnHayPile(int cc, int cr)
        {
            float leftover = _hayGrid[cc, cr] - config.hayUnitsPerCollectionCell;
            _hayGrid[cc, cr] = leftover;

            Vector3 cellCenter = CollectionCellCenter(cc, cr);

            if (hayPilePrefab != null)
            {
                // Host/server authority checked by caller in co-op (NetworkObject)
                var pile = Instantiate(hayPilePrefab, cellCenter, Quaternion.identity);
                OnHayPileSpawned?.Invoke(cellCenter, leftover);
            }

            UpdateDecal(cc, cr);
        }

        void UpdateDecal(int cc, int cr)
        {
            float fillRatio = _hayGrid[cc, cr] / config.hayUnitsPerCollectionCell;

            // In Phase 0 implementation, decals are placeholder — just log state.
            // Phase 1 will swap between 3 material phases.
            int phase = fillRatio < decalPhase2Threshold ? 0 :
                        fillRatio < decalPhase3Threshold ? 1 : 2;

            // Placeholder: real decal swap done in Phase 1
            _ = phase;
        }

        Vector3 CollectionCellCenter(int cc, int cr)
        {
            float lx = cc * config.collectionCellSize + config.collectionCellSize * 0.5f
                       - _grassField.fieldSize.x * 0.5f;
            float lz = cr * config.collectionCellSize + config.collectionCellSize * 0.5f
                       - _grassField.fieldSize.y * 0.5f;
            return transform.TransformPoint(new Vector3(lx, 0f, lz));
        }

        /// <summary>Returns the hay accumulation grid for saving (value = unit count in each cell).</summary>
        public float[,] GetHayGrid() => (float[,])_hayGrid.Clone();

        /// <summary>Restores hay grid from save data.</summary>
        public void LoadHayGrid(float[,] saved)
        {
            for (int r = 0; r < _collRows; r++)
                for (int c = 0; c < _collCols; c++)
                    _hayGrid[c, r] = saved[c, r];
        }

        void OnDrawGizmosSelected()
        {
            if (config == null || _hayGrid == null) return;
            for (int cr = 0; cr < _collRows; cr++)
                for (int cc = 0; cc < _collCols; cc++)
                {
                    float fill = _hayGrid[cc, cr] / config.hayUnitsPerCollectionCell;
                    Gizmos.color = new Color(1f, 0.8f, 0f, fill * 0.7f + 0.05f);
                    Vector3 center = CollectionCellCenter(cc, cr);
                    Gizmos.DrawCube(center,
                        new Vector3(config.collectionCellSize * 0.9f, 0.05f,
                                    config.collectionCellSize * 0.9f));
                }
        }
    }
}
using System;
using System.Collections.Generic;
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

        GrassField _grassField;

        float[,] _hayGrid;
        int _collCols;
        int _collRows;
        readonly List<GameObject> _spawnedPiles = new List<GameObject>();

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
        }

        int _totalCutCells;

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
            _totalCutCells++;
            if (_totalCutCells % 20 == 0)
                Debug.Log($"[Hay:{gameObject.name}] {_totalCutCells} cells cut, cell({cc},{cr}) has {_hayGrid[cc,cr]:F0}/{config.hayUnitsPerCollectionCell} units");

            // Only auto-spawn if a prefab is assigned; otherwise hay accumulates freely for manual baling.
            if (hayPilePrefab != null && _hayGrid[cc, cr] >= config.hayUnitsPerCollectionCell)
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
                var pile = Instantiate(hayPilePrefab, cellCenter, Quaternion.identity);
                _spawnedPiles.Add(pile);
                OnHayPileSpawned?.Invoke(cellCenter, leftover);
                Fields.Core.GameEvents.FireHayPileSpawned(_grassField.parcelIndex, cellCenter);
            }
        }

        Vector3 CollectionCellCenter(int cc, int cr)
        {
            // Field origin = GO local (0,0) — bottom-left, no centering offset.
            float lx = cc * config.collectionCellSize + config.collectionCellSize * 0.5f;
            float lz = cr * config.collectionCellSize + config.collectionCellSize * 0.5f;
            return transform.TransformPoint(new Vector3(lx, 0f, lz));
        }

        /// <summary>Clears all accumulated hay — used by editor reset.</summary>
        public void ResetHay()
        {
            for (int r = 0; r < _collRows; r++)
                for (int c = 0; c < _collCols; c++)
                    _hayGrid[c, r] = 0f;
        }

        /// <summary>
        /// Clears accumulated hay AND destroys all spawned but uncollected hay piles.
        /// Called on loop ending so the player must re-cut the regrown grass before baling.
        /// </summary>
        public void ResetWithPiles()
        {
            ResetHay();
            for (int i = _spawnedPiles.Count - 1; i >= 0; i--)
            {
                if (_spawnedPiles[i] != null)
                    Destroy(_spawnedPiles[i]);
            }
            _spawnedPiles.Clear();
        }

        /// <summary>
        /// Returns total hay units in all collection cells whose centre lies within radius of worldPos (XZ only).
        /// </summary>
        public float GetHayInRadius(Vector3 worldPos, float radius)
        {
            if (_hayGrid == null) return 0f;
            float total = 0f;
            float radiusSq = radius * radius;
            for (int r = 0; r < _collRows; r++)
                for (int c = 0; c < _collCols; c++)
                {
                    if (_hayGrid[c, r] <= 0f) continue;
                    Vector3 center = CollectionCellCenter(c, r);
                    float dx = worldPos.x - center.x;
                    float dz = worldPos.z - center.z;
                    if (dx * dx + dz * dz <= radiusSq) total += _hayGrid[c, r];
                }
            return total;
        }

        /// <summary>
        /// Removes up to maxAmount hay from cells within radius (closest cells first), updates decals.
        /// Returns actually consumed amount.
        /// </summary>
        public float ConsumeHayInRadius(Vector3 worldPos, float radius, float maxAmount)
        {
            if (_hayGrid == null) return 0f;
            float consumed = 0f;
            float radiusSq = radius * radius;
            for (int r = 0; r < _collRows && consumed < maxAmount; r++)
                for (int c = 0; c < _collCols && consumed < maxAmount; c++)
                {
                    if (_hayGrid[c, r] <= 0f) continue;
                    Vector3 center = CollectionCellCenter(c, r);
                    float dx = worldPos.x - center.x;
                    float dz = worldPos.z - center.z;
                    if (dx * dx + dz * dz > radiusSq) continue;
                    float take = Mathf.Min(_hayGrid[c, r], maxAmount - consumed);
                    _hayGrid[c, r] -= take;
                    consumed += take;
                }
            return consumed;
        }

        /// <summary>Returns the hay accumulation grid for saving (value = unit count in each cell).</summary>
        public float[,] GetHayGrid() => _hayGrid != null ? (float[,])_hayGrid.Clone() : new float[0, 0];

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
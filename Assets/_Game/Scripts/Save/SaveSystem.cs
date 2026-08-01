using System.IO;
using Fields.Economy;
using Fields.Grass;
using Fields.Hay;
using UnityEngine;

namespace Fields.Save
{
    /// <summary>
    /// Manages save/load. Autosaves on: parcel complete / purchase / bale sold / 60s.
    /// Data written to Application.persistentDataPath as JSON.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Header("Scene references (match parcel order 0-3)")]
        public GrassField[] grassFields = new GrassField[4];
        public HayAccumulationSystem[] hayAccumulationSystems = new HayAccumulationSystem[4];

        const string SAVE_FILE = "fields_save.json";
        const float AUTOSAVE_INTERVAL = 60f;

        float _autosaveTimer;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Hook autosave triggers
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged += (_, __) => TriggerAutosave("purchase");
        }

        void Update()
        {
            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer >= AUTOSAVE_INTERVAL)
            {
                _autosaveTimer = 0f;
                SaveGame();
            }
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        public void SaveGame()
        {
            var data = BuildSaveData();
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            string path = SavePath();
            File.WriteAllText(path, json);
            Fields.Core.SteamManager.Instance?.CloudSave(json); // mirror to Steam Cloud
            Debug.Log($"[SaveSystem] Saved to {path}");
        }

        public bool LoadGame()
        {
            // Try Steam Cloud first
            string cloudJson = Fields.Core.SteamManager.Instance?.CloudLoad();
            string json = null;

            if (!string.IsNullOrEmpty(cloudJson))
            {
                json = cloudJson;
            }
            else
            {
                string path = SavePath();
                if (!File.Exists(path)) return false;
                json = File.ReadAllText(path);
            }

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch { Debug.LogWarning("[SaveSystem] Failed to parse save file."); return false; }

            if (data.version != 1)
            {
                Debug.LogWarning($"[SaveSystem] Unknown save version {data.version}.");
                return false;
            }

            ApplySaveData(data);
            return true;
        }

        public void DeleteSave()
        {
            string path = SavePath();
            if (File.Exists(path)) File.Delete(path);
        }

        // ------------------------------------------------------------------ //
        // Internal
        // ------------------------------------------------------------------ //

        SaveData BuildSaveData()
        {
            var cm = CurrencyManager.Instance;
            var um = ToolUnlockManager.Instance;
            var pm = ParcelManager.Instance;
            var bm = Fields.Economy.BalerManager.Instance;

            var data = new SaveData
            {
                money = cm != null ? cm.Money : 0,
                toolsOwned = um != null ? um.GetOwnedArray() : new bool[5],
                toolUpgradeLevels = um != null ? um.GetLevelsArray() : new int[5],
                parcelsUnlocked = pm != null ? pm.GetUnlockedArray() : new bool[4],
                roundBalerOwned = bm != null && bm.GetRoundBalerOwned(),
                balerUpgradeLevels = bm != null ? bm.GetLevels() : new int[3],
            };

            // Grass + hay grids
            data.fields = new FieldSaveData[grassFields.Length];
            for (int i = 0; i < grassFields.Length; i++)
            {
                var gf = grassFields[i];
                if (gf == null) continue;
                var grid = gf.GetCutGrid();
                var fd = new FieldSaveData
                {
                    parcelIndex = i,
                    gridCols    = gf.GridCols,
                    gridRows    = gf.GridRows,
                    cutGridRLE  = RLEEncoder.Encode(grid, gf.GridCols, gf.GridRows)
                };

                var hay = i < hayAccumulationSystems.Length ? hayAccumulationSystems[i] : null;
                if (hay != null)
                {
                    float[,] hayGrid = hay.GetHayGrid();
                    int cols = hayGrid.GetLength(0);
                    int rows = hayGrid.GetLength(1);
                    fd.hayCollCols = cols;
                    fd.hayCollRows = rows;
                    fd.hayGrid = new float[cols * rows];
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            fd.hayGrid[c + r * cols] = hayGrid[c, r];
                }

                data.fields[i] = fd;
            }

            return data;
        }

        void ApplySaveData(SaveData data)
        {
            CurrencyManager.Instance?.SetMoney(data.money);
            ToolUnlockManager.Instance?.LoadState(data.toolsOwned, data.toolUpgradeLevels);
            ParcelManager.Instance?.LoadState(data.parcelsUnlocked);
            Fields.Economy.BalerManager.Instance?.LoadState(data.roundBalerOwned, data.balerUpgradeLevels);

            for (int i = 0; i < grassFields.Length; i++)
            {
                if (data.fields == null || i >= data.fields.Length) continue;
                var fd = data.fields[i];
                if (fd == null) continue;

                var gf = i < grassFields.Length ? grassFields[i] : null;
                if (gf != null && fd.cutGridRLE != null)
                {
                    var grid = RLEEncoder.Decode(fd.cutGridRLE, fd.gridCols, fd.gridRows);
                    gf.LoadCutGrid(grid);
                }

                var hay = i < hayAccumulationSystems.Length ? hayAccumulationSystems[i] : null;
                if (hay != null && fd.hayGrid != null && fd.hayCollCols > 0 && fd.hayCollRows > 0)
                {
                    int cols = fd.hayCollCols, rows = fd.hayCollRows;
                    var hayGrid = new float[cols, rows];
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            hayGrid[c, r] = fd.hayGrid[c + r * cols];
                    hay.LoadHayGrid(hayGrid);
                }
            }
        }

        void TriggerAutosave(string reason)
        {
            _autosaveTimer = 0f;
            SaveGame();
        }

        static string SavePath() =>
            Path.Combine(Application.persistentDataPath, SAVE_FILE);
    }
}
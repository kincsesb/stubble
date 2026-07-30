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

        [Header("Scene references")]
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
            string path = SavePath();
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: false));
            Debug.Log($"[SaveSystem] Saved to {path}");
        }

        public bool LoadGame()
        {
            string path = SavePath();
            if (!File.Exists(path)) return false;

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
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

            var data = new SaveData
            {
                money = cm != null ? cm.Money : 0,
                toolsOwned = um != null ? um.GetOwnedArray() : new bool[5],
                toolUpgradeLevels = um != null ? um.GetLevelsArray() : new int[5],
                parcelsUnlocked = pm != null ? pm.GetUnlockedArray() : new bool[4],
            };

            // Grass grids
            data.fields = new FieldSaveData[grassFields.Length];
            for (int i = 0; i < grassFields.Length; i++)
            {
                var gf = grassFields[i];
                if (gf == null) continue;
                var grid = gf.GetCutGrid();
                data.fields[i] = new FieldSaveData
                {
                    parcelIndex = i,
                    gridCols = gf.GridCols,
                    gridRows = gf.GridRows,
                    cutGridRLE = RLEEncoder.Encode(grid, gf.GridCols, gf.GridRows)
                };
            }

            return data;
        }

        void ApplySaveData(SaveData data)
        {
            CurrencyManager.Instance?.SetMoney(data.money);
            ToolUnlockManager.Instance?.LoadState(data.toolsOwned, data.toolUpgradeLevels);
            ParcelManager.Instance?.LoadState(data.parcelsUnlocked);

            for (int i = 0; i < grassFields.Length; i++)
            {
                var gf = grassFields[i];
                if (gf == null || data.fields == null || i >= data.fields.Length) continue;
                var fd = data.fields[i];
                if (fd?.cutGridRLE == null) continue;
                var grid = RLEEncoder.Decode(fd.cutGridRLE, fd.gridCols, fd.gridRows);
                gf.LoadCutGrid(grid);
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
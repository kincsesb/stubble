using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fields.Core;
using Fields.Economy;
using Fields.Grass;
using Fields.Hay;
using UnityEngine;

namespace Fields.Save
{
    /// <summary>
    /// Versioned save/load with atomic writes, rolling backup, and background
    /// JSON serialization. Never causes a frame hitch.
    ///
    /// File layout (Application.persistentDataPath):
    ///   save_slot0.dat  — game state   (this system)
    ///   save_slot0.bak  — rolling backup of previous successful save
    ///   settings.json   — user settings (managed by SettingsManager, TBD)
    ///   records.json    — personal bests (managed by RecordsManager, TBD)
    ///
    /// Autosave triggers: parcel complete, purchase, bale sold, every 60 s.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Header("Scene references — match parcel order 0-3")]
        public GrassField[]            grassFields            = new GrassField[4];
        public HayAccumulationSystem[] hayAccumulationSystems = new HayAccumulationSystem[4];

        [Header("Prefabs for bale restoration on load")]
        public GameObject squareBalePrefab;
        public GameObject roundBalePrefab;

        // ── File paths ─────────────────────────────────────────────────────── //
        const string SAVE_FILE = "save_slot0.dat";
        const string SAVE_TEMP = "save_slot0.tmp";
        const string SAVE_BACK = "save_slot0.bak";

        // ── Autosave ───────────────────────────────────────────────────────── //
        const float AUTOSAVE_INTERVAL = 60f;
        float _autosaveTimer;

        /// <summary>True if a valid save file exists on disk.</summary>
        public static bool HasSave =>
            File.Exists(Path.Combine(Application.persistentDataPath, SAVE_FILE));

        // ── Background save state (accessed from two threads — volatile) ───── //
        volatile bool _saveInFlight;
        volatile bool _saveJustCompleted;

        // Set by DeleteSave() — blocks all further writes so OnApplicationQuit
        // can't recreate the file after a nuclear ending wipe.
        bool _saveDeleted;

        public bool IsSaveInFlight => _saveInFlight;

        /// <summary>Fired on the main thread when an autosave completes.</summary>
        public static event Action OnSaveCompleted;

        // ================================================================== //
        // Lifecycle
        // ================================================================== //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            HookAutosaveEvents();
        }

        void OnApplicationQuit() { if (!_saveDeleted) SaveGame(); }

        void Update()
        {
            // Dispatch save-complete notification back on the main thread.
            if (_saveJustCompleted)
            {
                _saveJustCompleted = false;
                OnSaveCompleted?.Invoke();
            }

            // Autosave timer — only tick while gameplay is running.
            if (Time.timeScale > 0f && !_saveInFlight)
            {
                _autosaveTimer += Time.deltaTime;
                if (_autosaveTimer >= AUTOSAVE_INTERVAL)
                {
                    _autosaveTimer = 0f;
                    SaveGame();
                }
            }
        }

        void OnDestroy()
        {
            UnhookAutosaveEvents();
        }

        // ================================================================== //
        // Public API
        // ================================================================== //

        /// <summary>
        /// Serialise game state to disk. Serialization runs on a background
        /// thread; only the atomic file rename touches the filesystem at the end.
        /// Safe to call from any gameplay event — guards against stacking.
        /// </summary>
        public void SaveGame()
        {
            if (_saveInFlight || _saveDeleted) return;

            SaveData data     = BuildSaveData(); // must run on main thread
            string savePath   = SavePath();       // cache paths on main thread — persistentDataPath is main-thread-only
            string tempPath   = TempPath();
            string backupPath = BackupPath();
            _saveInFlight = true;

            var thread = new Thread(() =>
            {
                try
                {
                    string json = JsonUtility.ToJson(data, prettyPrint: false);
                    WriteAtomic(savePath, tempPath, backupPath, json);
                    Fields.Core.SteamManager.Instance?.CloudSave(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveSystem] Background save failed: {ex.Message}");
                }
                finally
                {
                    _saveInFlight     = false;
                    _saveJustCompleted = true;
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Load and apply save data. Returns false if no valid save exists.
        /// On version mismatch (save is newer than game), refuses to load and logs clearly.
        /// </summary>
        public bool LoadGame()
        {
            string json = TryReadSave();
            if (string.IsNullOrEmpty(json)) return false;

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Save parse failed: {ex.Message}. Trying backup.");
                return TryLoadBackup();
            }

            int detected = DetectVersion(data);
            if (detected > SaveData.CURRENT_VERSION)
            {
                Debug.LogError($"[SaveSystem] Save is version {detected} but game only supports up to {SaveData.CURRENT_VERSION}. Cannot load — please update the game.");
                return false;
            }

            RunMigrations(data, detected);
            ApplySaveData(data);
            return true;
        }

        public void DeleteSave()
        {
            _saveDeleted = true;
            string path = SavePath();
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Deletes the rolling backup file. Call alongside DeleteSave() for a full wipe.</summary>
        public void DeleteBackup()
        {
            string path = BackupPath();
            if (File.Exists(path)) File.Delete(path);
        }

        // ================================================================== //
        // Build
        // ================================================================== //

        SaveData BuildSaveData()
        {
            var cm = CurrencyManager.Instance;
            var um = ToolUnlockManager.Instance;
            var pm = ParcelManager.Instance;
            var bm = BalerManager.Instance;
            var ss = SessionState.Instance;

            var data = new SaveData
            {
                schemaVersion   = SaveData.CURRENT_VERSION,
                saveTimestamp   = DateTime.UtcNow.ToString("o"),
                totalPlaytime   = ss != null ? ss.TotalPlaytime : 0f,
                isMultiplayerSave = ss != null && ss.IsMultiplayer,

                money              = cm != null ? cm.Money : 0,
                toolsOwned         = um != null ? um.GetOwnedArray()  : new bool[5],
                toolUpgradeLevels  = um != null ? um.GetLevelsArray() : new int[5],
                parcelsUnlocked    = pm != null ? pm.GetUnlockedArray() : new bool[4],
                roundBalerOwned    = bm != null && bm.GetRoundBalerOwned(),
                balerUpgradeLevels = bm != null ? bm.GetLevels() : new int[3],
            };

            // ── World events ─────────────────────────────────────────────── //
            data.catAteChicken = FarmAnimalChaseSystem.ChickenWasEaten;

            // ── Player position ───────────────────────────────────────────── //
            var pc = Fields.Core.PlayerController.Instance;
            if (pc != null)
            {
                data.hasSavedPosition = true;
                data.playerPosX = pc.transform.position.x;
                data.playerPosY = pc.transform.position.y;
                data.playerPosZ = pc.transform.position.z;
                data.playerRotY = pc.transform.eulerAngles.y;
            }

            // ── Fields: cut grid + hay grid ──────────────────────────────── //
            data.fields = new FieldSaveData[grassFields.Length];
            for (int i = 0; i < grassFields.Length; i++)
            {
                var gf = grassFields[i];
                if (gf == null) continue;

                var pngBytes = gf.CaptureGpuMaskPng();
                var fd = new FieldSaveData
                {
                    parcelIndex  = i,
                    gridCols     = gf.GridCols,
                    gridRows     = gf.GridRows,
                    cutGridRLE   = RLEEncoder.Encode(gf.GetCutGrid(), gf.GridCols, gf.GridRows),
                    grassMaskPng = pngBytes != null ? System.Convert.ToBase64String(pngBytes) : null
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

            // ── Bales: all world bales not currently carried ─────────────── //
            data.bales = BuildBaleSaveData();

            // ── Statistics ───────────────────────────────────────────────── //
            data.playerStats = BuildPlayerStats(ss);
            data.parcelStats = BuildParcelStats(ss);

            return data;
        }

        static BaleSaveData[] BuildBaleSaveData()
        {
            var list = new List<BaleSaveData>();

            foreach (var sq in UnityEngine.Object.FindObjectsByType<SquareBale>(FindObjectsSortMode.None))
            {
                if (sq.IsCarried) continue;  // carried bales re-form on next baling session
                var t = sq.transform;
                list.Add(new BaleSaveData
                {
                    posX = t.position.x, posY = t.position.y, posZ = t.position.z,
                    rotX = t.rotation.x, rotY = t.rotation.y,
                    rotZ = t.rotation.z, rotW = t.rotation.w,
                    isRound            = false,
                    hayUnits           = sq.HayUnits,
                    originParcelIndex  = sq.OriginParcelIndex,
                });
            }

            foreach (var rb in UnityEngine.Object.FindObjectsByType<RoundBale>(FindObjectsSortMode.None))
            {
                var t       = rb.transform;
                Vector3 hdg = rb.GetHeading();
                list.Add(new BaleSaveData
                {
                    posX = t.position.x, posY = t.position.y, posZ = t.position.z,
                    rotX = t.rotation.x, rotY = t.rotation.y,
                    rotZ = t.rotation.z, rotW = t.rotation.w,
                    isRound           = true,
                    hayUnits          = rb.HayUnits,
                    originParcelIndex = 0,
                    headingX          = hdg.x,
                    headingZ          = hdg.z,
                    rollAngle         = rb.GetRollAngle(),
                });
            }

            return list.ToArray();
        }

        static PlayerSaveStats[] BuildPlayerStats(SessionState ss)
        {
            if (ss == null) return new PlayerSaveStats[1] { new PlayerSaveStats { playerId = 0 } };
            var stats = new PlayerSaveStats[ss.PlayerCount];
            for (int i = 0; i < ss.PlayerCount; i++)
            {
                var p = ss.GetPlayer(i);
                stats[i] = p == null ? new PlayerSaveStats { playerId = i } : new PlayerSaveStats
                {
                    playerId           = p.PlayerId,
                    areaCutCells       = p.AreaCutCells,
                    hayPilesCollected  = p.HayPilesCollected,
                    squareBalesMade    = p.SquareBalesMade,
                    roundBalesMade     = p.RoundBalesMade,
                    moneyEarned        = p.MoneyEarned,
                    moneySpent         = p.MoneySpent,
                    totalSwings        = p.TotalSwings,
                    distanceTravelledM = p.DistanceTravelledM,
                    playtimeSeconds    = p.PlaytimeSeconds,
                };
            }
            return stats;
        }

        static ParcelSaveStats[] BuildParcelStats(SessionState ss)
        {
            var stats = new ParcelSaveStats[4];
            for (int i = 0; i < 4; i++)
            {
                var p = ss?.GetParcel(i);
                stats[i] = p == null ? new ParcelSaveStats { parcelIndex = i } : new ParcelSaveStats
                {
                    parcelIndex        = i,
                    areaCutCells       = p.AreaCutCells,
                    hayPilesSpawned    = p.HayPilesSpawned,
                    hayPilesCollected  = p.HayPilesCollected,
                    squareBalesMade    = p.SquareBalesMade,
                    roundBalesMade     = p.RoundBalesMade,
                    moneyEarned        = p.MoneyEarned,
                    timeSpentSeconds   = p.TimeSpentSeconds,
                    grassCutComplete   = p.GrassCutComplete,
                    hayClearComplete   = p.HayClearComplete,
                };
            }
            return stats;
        }

        // ================================================================== //
        // Apply
        // ================================================================== //

        void ApplySaveData(SaveData data)
        {
            FarmAnimalChaseSystem.ChickenWasEaten = data.catAteChicken;

            if (data.hasSavedPosition)
            {
                Fields.Core.PlayerController.PendingSpawnPosition = new Vector3(data.playerPosX, data.playerPosY, data.playerPosZ);
                Fields.Core.PlayerController.PendingSpawnRotY     = data.playerRotY;
            }
            else
            {
                Fields.Core.PlayerController.PendingSpawnPosition = null;
            }

            CurrencyManager.Instance?.SetMoney(data.money);
            ToolUnlockManager.Instance?.LoadState(data.toolsOwned, data.toolUpgradeLevels);
            ParcelManager.Instance?.LoadState(data.parcelsUnlocked);
            BalerManager.Instance?.LoadState(data.roundBalerOwned, data.balerUpgradeLevels);

            // Fields
            for (int i = 0; i < grassFields.Length; i++)
            {
                if (data.fields == null || i >= data.fields.Length) continue;
                var fd = data.fields[i];
                if (fd == null) continue;

                var gf = grassFields[i];
                if (gf != null && fd.cutGridRLE != null)
                {
                    var grid = RLEEncoder.Decode(fd.cutGridRLE, fd.gridCols, fd.gridRows);
                    if (!string.IsNullOrEmpty(fd.grassMaskPng))
                    {
                        // Fast path: set CPU grid, restore GPU from PNG snapshot (1 blit)
                        gf.LoadCutGridCpuOnly(grid);
                        gf.RestoreGpuMask(System.Convert.FromBase64String(fd.grassMaskPng));
                    }
                    else
                    {
                        // Backward compat: no snapshot → capsule-based GPU rebuild
                        gf.LoadCutGrid(grid);
                    }
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

            // Bales
            if (data.bales != null)
                foreach (var bd in data.bales)
                    SpawnBale(bd);

            // Statistics → SessionState
            var ss = SessionState.Instance;
            if (ss != null)
            {
                ss.LoadTotalPlaytime(data.totalPlaytime);
                if (data.playerStats != null)
                    foreach (var ps in data.playerStats)
                    {
                        var p = ss.GetPlayer(ps.playerId);
                        if (p == null) continue;
                        p.AreaCutCells      = ps.areaCutCells;
                        p.HayPilesCollected = ps.hayPilesCollected;
                        p.SquareBalesMade   = ps.squareBalesMade;
                        p.RoundBalesMade    = ps.roundBalesMade;
                        p.MoneyEarned       = ps.moneyEarned;
                        p.MoneySpent        = ps.moneySpent;
                        p.TotalSwings       = ps.totalSwings;
                        p.DistanceTravelledM = ps.distanceTravelledM;
                        p.PlaytimeSeconds   = ps.playtimeSeconds;
                    }

                if (data.parcelStats != null)
                    foreach (var ps in data.parcelStats)
                    {
                        var p = ss.GetParcel(ps.parcelIndex);
                        if (p == null) continue;
                        p.AreaCutCells      = ps.areaCutCells;
                        p.HayPilesSpawned   = ps.hayPilesSpawned;
                        p.HayPilesCollected = ps.hayPilesCollected;
                        p.SquareBalesMade   = ps.squareBalesMade;
                        p.RoundBalesMade    = ps.roundBalesMade;
                        p.MoneyEarned       = ps.moneyEarned;
                        p.TimeSpentSeconds  = ps.timeSpentSeconds;
                        p.GrassCutComplete  = ps.grassCutComplete;
                        p.HayClearComplete  = ps.hayClearComplete;
                    }
            }
        }

        void SpawnBale(BaleSaveData bd)
        {
            var prefab = bd.isRound ? roundBalePrefab : squareBalePrefab;
            if (prefab == null) return;

            var pos = new Vector3(bd.posX, bd.posY, bd.posZ);
            var rot = new Quaternion(bd.rotX, bd.rotY, bd.rotZ, bd.rotW);
            var go  = Instantiate(prefab, pos, rot);

            if (!bd.isRound)
            {
                var sq = go.GetComponent<SquareBale>();
                if (sq != null)
                {
                    sq.hayUnits          = bd.hayUnits;
                    sq.OriginParcelIndex = bd.originParcelIndex;
                }
            }
            else
            {
                var rb = go.GetComponent<RoundBale>();
                if (rb != null) rb.hayUnits = bd.hayUnits;
                var ctrl = go.GetComponent<Fields.Hay.RoundBaleController>();
                if (ctrl != null)
                {
                    Vector3 heading = new Vector3(bd.headingX, 0f, bd.headingZ);
                    if (heading.sqrMagnitude < 0.001f) heading = Vector3.forward;
                    ctrl.SetStateFromSave(heading, bd.rollAngle);
                }
            }
        }

        // ================================================================== //
        // Migration
        // ================================================================== //

        static int DetectVersion(SaveData data)
        {
            if (data.schemaVersion > 0) return data.schemaVersion;
            if (data.version       > 0) return data.version;
            return 0;
        }

        static void RunMigrations(SaveData data, int fromVersion)
        {
            if (fromVersion < 2) MigrateV1ToV2(data);
            if (fromVersion < 3) MigrateV2ToV3(data);
        }

        /// <summary>
        /// Migrate a v1 save in-place.
        /// Rules: never lose existing data; derive what we can; zero the rest.
        /// All existing money is attributed to player 0.
        ///
        /// Unit-testable: pass a synthetic SaveData with version=1 and assert
        /// schemaVersion==2 and parcelStats[i].areaCutCells > 0 after calling this.
        /// </summary>
        internal static void MigrateV1ToV2(SaveData data)
        {
            // ── Player stats ──────────────────────────────────────────────── //
            data.playerStats = new PlayerSaveStats[1];
            data.playerStats[0] = new PlayerSaveStats
            {
                playerId    = 0,
                moneyEarned = data.money,  // best-effort: attribute all money to p0
            };

            // ── Parcel stats — derive area cut from existing RLE grids ────── //
            data.parcelStats = new ParcelSaveStats[4];
            long totalCutCells = 0;
            for (int i = 0; i < 4; i++)
            {
                data.parcelStats[i] = new ParcelSaveStats { parcelIndex = i };
                if (data.fields == null || i >= data.fields.Length) continue;
                var fd = data.fields[i];
                if (fd?.cutGridRLE == null || fd.gridCols == 0 || fd.gridRows == 0) continue;

                var grid = RLEEncoder.Decode(fd.cutGridRLE, fd.gridCols, fd.gridRows);
                long cut = 0;
                for (int r = 0; r < fd.gridRows; r++)
                    for (int c = 0; c < fd.gridCols; c++)
                        if (!grid[c, r]) cut++;  // false = cell has been cut

                data.parcelStats[i].areaCutCells = cut;
                totalCutCells += cut;
            }
            data.playerStats[0].areaCutCells = totalCutCells;

            // ── Stamp new version ─────────────────────────────────────────── //
            data.schemaVersion = SaveData.CURRENT_VERSION;
            data.version       = 0;  // clear v1 marker
        }

        /// <summary>
        /// v2→v3: adds heading/rollAngle defaults to existing round bales.
        /// No data is lost — square bale fields are untouched.
        /// </summary>
        internal static void MigrateV2ToV3(SaveData data)
        {
            if (data.bales != null)
                foreach (var bd in data.bales)
                    if (bd.isRound && bd.headingX == 0f && bd.headingZ == 0f)
                    {
                        // Derive forward from saved quaternion rotation
                        var rot = new Quaternion(bd.rotX, bd.rotY, bd.rotZ, bd.rotW);
                        Vector3 fwd = rot * Vector3.forward;
                        fwd.y = 0f;
                        if (fwd.sqrMagnitude > 0.001f) fwd.Normalize(); else fwd = Vector3.forward;
                        bd.headingX = fwd.x;
                        bd.headingZ = fwd.z;
                        bd.rollAngle = 0f;
                    }
            data.schemaVersion = SaveData.CURRENT_VERSION;
        }

        // ================================================================== //
        // Atomic file I/O
        // ================================================================== //

        /// <summary>
        /// Write-to-temp → backup-existing → rename-over-real.
        /// A crash mid-write leaves the temp file; the real file is intact.
        /// </summary>
        static void WriteAtomic(string realPath, string tempPath, string backupPath, string json)
        {
            // 1. Write to temp (crash here → temp is partial, real is intact)
            File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

            // 2. Rotate existing save to backup (crash here → temp + real both exist, backup may be gone)
            if (File.Exists(realPath))
                File.Copy(realPath, backupPath, overwrite: true);

            // 3. Atomic rename (on POSIX systems this is truly atomic; on Windows it's near-atomic)
            if (File.Exists(realPath)) File.Delete(realPath);
            File.Move(tempPath, realPath);
        }

        // ================================================================== //
        // Load helpers
        // ================================================================== //

        string TryReadSave()
        {
            // Steam Cloud first
            string cloud = Fields.Core.SteamManager.Instance?.CloudLoad();
            if (!string.IsNullOrEmpty(cloud)) return cloud;

            string path = SavePath();
            if (File.Exists(path))
            {
                try { return File.ReadAllText(path, System.Text.Encoding.UTF8); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveSystem] Cannot read save: {ex.Message}. Trying backup.");
                }
            }
            return null;
        }

        bool TryLoadBackup()
        {
            string backupPath = BackupPath();
            if (!File.Exists(backupPath))
            {
                Debug.Log("[SaveSystem] No backup found.");
                return false;
            }

            string json;
            try { json = File.ReadAllText(backupPath, System.Text.Encoding.UTF8); }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Backup also unreadable: {ex.Message}");
                return false;
            }

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch
            {
                Debug.LogError("[SaveSystem] Backup is corrupt.");
                return false;
            }

            Debug.LogWarning("[SaveSystem] Loaded from backup save.");
            int detected = DetectVersion(data);
            RunMigrations(data, detected);
            ApplySaveData(data);
            return true;
        }

        // ================================================================== //
        // Autosave event wiring
        // ================================================================== //

        void HookAutosaveEvents()
        {
            GameEvents.OnBaleSold      += OnBaleSoldForSave;
            GameEvents.OnToolPurchased += OnToolPurchasedForSave;
            GameEvents.OnParcelUnlocked += OnParcelUnlockedForSave;
        }

        void UnhookAutosaveEvents()
        {
            GameEvents.OnBaleSold       -= OnBaleSoldForSave;
            GameEvents.OnToolPurchased  -= OnToolPurchasedForSave;
            GameEvents.OnParcelUnlocked -= OnParcelUnlockedForSave;
        }

        void OnBaleSoldForSave(int _, int __, int ___) => TriggerAutosave();
        void OnToolPurchasedForSave(int _, int __)     => TriggerAutosave();
        void OnParcelUnlockedForSave(int _, int __)    => TriggerAutosave();

        void TriggerAutosave()
        {
            _autosaveTimer = 0f;
            SaveGame();
        }

        // ================================================================== //
        // Paths
        // ================================================================== //

        static string SavePath()   => Path.Combine(Application.persistentDataPath, SAVE_FILE);
        static string TempPath()   => Path.Combine(Application.persistentDataPath, SAVE_TEMP);
        static string BackupPath() => Path.Combine(Application.persistentDataPath, SAVE_BACK);
    }
}
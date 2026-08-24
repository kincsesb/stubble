using System;

namespace Fields.Save
{
    // ======================================================================= //
    // Schema v2 — current version.
    //
    // Versioning strategy:
    //   v1 save JSON contains "version":1 (the old field name).
    //   v2+ save JSON contains "schemaVersion":2 (new field name).
    //   Both fields live in the class so a single JsonUtility.FromJson pass
    //   can load either format; the loader then migrates as needed.
    //
    // Adding v3: increment CURRENT_VERSION, add a MigrateV2ToV3 step,
    // register it in SaveSystem.RunMigrations().
    // ======================================================================= //

    [Serializable]
    public class SaveData
    {
        public const int CURRENT_VERSION = 3;

        // v1 compat: old saves have "version":1, new saves leave this 0.
        public int version = 0;
        // v2+: canonical version field.
        public int schemaVersion = CURRENT_VERSION;

        // ── Session ────────────────────────────────────────────────────────────
        public string saveTimestamp   = "";   // ISO-8601 UTC
        public float  totalPlaytime   = 0f;   // seconds, unscaled
        public bool   isMultiplayerSave = false;

        // ── Economy ────────────────────────────────────────────────────────────
        public int    money;
        public bool[] toolsOwned          = new bool[5];
        public int[]  toolUpgradeLevels   = new int[5];
        public bool[] parcelsUnlocked     = new bool[4];
        public bool   roundBalerOwned;
        /// <summary>[0]=balerLevel, [1]=hayValueLevel, [2]=reserved</summary>
        public int[]  balerUpgradeLevels  = new int[3];

        // ── World geometry ─────────────────────────────────────────────────────
        public FieldSaveData[] fields = new FieldSaveData[4];

        // ── World objects ──────────────────────────────────────────────────────
        public BaleSaveData[] bales = new BaleSaveData[0];

        // ── World events ───────────────────────────────────────────────────────
        public bool catAteChicken;

        // ── Player spawn position ──────────────────────────────────────────────
        public bool  hasSavedPosition;
        public float playerPosX, playerPosY, playerPosZ;
        public float playerRotY;

        // ── Statistics (new in v2) ─────────────────────────────────────────────
        public PlayerSaveStats[] playerStats = new PlayerSaveStats[0];
        public ParcelSaveStats[] parcelStats = new ParcelSaveStats[0];
    }

    // ── Field data ─────────────────────────────────────────────────────────── //

    [Serializable]
    public class FieldSaveData
    {
        public int parcelIndex;
        public int gridCols;
        public int gridRows;
        /// <summary>RLE-encoded cut grid. Each int: (value &lt;&lt; 24 | count).</summary>
        public int[] cutGridRLE;
        /// <summary>Base64-encoded PNG snapshot of the GPU mask RT. Null = not present, fall back to RLE rebuild.</summary>
        public string grassMaskPng;
        // Hay accumulation grid (col-major float[], size = collCols * collRows)
        public int   hayCollCols;
        public int   hayCollRows;
        public float[] hayGrid;
    }

    // ── Bale world object ──────────────────────────────────────────────────── //

    [Serializable]
    public class BaleSaveData
    {
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
        public bool  isRound;
        public int   hayUnits;
        /// <summary>Parcel where the hay was originally cut (for income attribution).</summary>
        public int   originParcelIndex;
        // v3: round-bale controller state (heading XZ, accumulated roll)
        public float headingX, headingZ;
        public float rollAngle;
    }

    // ── Per-player statistics (v2) ─────────────────────────────────────────── //

    [Serializable]
    public class PlayerSaveStats
    {
        public int    playerId;
        public long   areaCutCells;
        public long   hayPilesCollected;
        public long   squareBalesMade;
        public long   roundBalesMade;
        public long   moneyEarned;
        public long   moneySpent;
        public long   totalSwings;
        public double distanceTravelledM;
        public float  playtimeSeconds;
    }

    // ── Per-parcel statistics (v2) ─────────────────────────────────────────── //

    [Serializable]
    public class ParcelSaveStats
    {
        public int   parcelIndex;
        public long  areaCutCells;
        public long  hayPilesSpawned;
        public long  hayPilesCollected;
        public long  squareBalesMade;
        public long  roundBalesMade;
        public long  moneyEarned;
        public float timeSpentSeconds;
        public bool  grassCutComplete;
        public bool  hayClearComplete;
    }
}
using System;

namespace Fields.Save
{
    /// <summary>
    /// Versioned save schema. All data serialised as JSON via JsonUtility.
    /// RLE encoding for grass cut grids — ≥80% size reduction vs raw bool[].
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int money;
        public bool[] toolsOwned = new bool[5];
        public int[] toolUpgradeLevels = new int[5];
        public bool[] parcelsUnlocked = new bool[4];
        public bool roundBalerOwned;
        public int[] balerUpgradeLevels = new int[3];
        public FieldSaveData[] fields = new FieldSaveData[4];
        public BaleSaveData[] bales = new BaleSaveData[0];
        public HayPileSaveData[] hayPiles = new HayPileSaveData[0];
        public float[] hayGrids = new float[0]; // flattened: [parcel][collRow][collCol]
    }

    [Serializable]
    public class FieldSaveData
    {
        public int parcelIndex;
        public int gridCols;
        public int gridRows;
        /// <summary>RLE encoded cut grid. Each entry: (value << 24 | count).</summary>
        public int[] cutGridRLE;
        // Hay accumulation grid (col-major flattened float[], size = collCols * collRows)
        public int hayCollCols;
        public int hayCollRows;
        public float[] hayGrid;
    }

    [Serializable]
    public class BaleSaveData
    {
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
        public bool isRound;
        public int hayUnits;
        public int parcelIndex;
    }

    [Serializable]
    public class HayPileSaveData
    {
        public float posX, posY, posZ;
        public int hayUnits;
    }
}
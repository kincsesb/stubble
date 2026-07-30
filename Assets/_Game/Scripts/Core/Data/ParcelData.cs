using UnityEngine;

namespace Fields.Core.Data
{
    [CreateAssetMenu(fileName = "ParcelData", menuName = "Fields/Parcel Data")]
    public class ParcelData : ScriptableObject
    {
        public string parcelName;
        public int unlockCost;
        [Tooltip("Total cuttable area in m²")]
        public float cuttableArea;
        [Tooltip("Target completion time in minutes for scoring")]
        public float targetTimeMinutes;
        [Tooltip("Index of this parcel (0-based)")]
        public int parcelIndex;
    }
}
using UnityEngine;

namespace Fields.Core.Data
{
    [CreateAssetMenu(fileName = "ToolData", menuName = "Fields/Tool Data")]
    public class ToolData : ScriptableObject
    {
        [Header("Identity")]
        public string toolName;
        public Sprite icon;
        public ToolType toolType;

        [Header("Per-level stats (index 0 = base, 1-3 = upgrade levels)")]
        [Tooltip("Movement speed multiplier while using this tool")]
        public float[] speedLevels = { 1f, 1.1f, 1.2f, 1.35f };

        [Tooltip("Stamina/fuel pool size")]
        public float[] enduranceLevels = { 100f, 120f, 145f, 175f };

        [Tooltip("Cut radius or arc width multiplier")]
        public float[] powerLevels = { 1f, 1.15f, 1.3f, 1.5f };

        [Header("Economy")]
        public int purchaseCost;
        [Tooltip("Cost to upgrade from level 0→1, 1→2, 2→3")]
        public int[] upgradeCosts = { 50, 150, 400 };
    }

    public enum ToolType
    {
        HandSickle,
        LongScythe,
        StringTrimmer,
        PushMower,
        RideOnMower
    }
}
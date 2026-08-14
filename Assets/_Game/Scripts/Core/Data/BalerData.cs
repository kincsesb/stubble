using UnityEngine;

namespace Fields.Core.Data
{
    [CreateAssetMenu(fileName = "BalerData", menuName = "Fields/Baler Data")]
    public class BalerData : ScriptableObject
    {
        [Header("Type")]
        public bool isRoundBaler;

        [Header("Per-level stats (index 0 = base, 1-3 = upgrade levels)")]
        public float[] compressionSpeedLevels = { 1f, 1.2f, 1.45f, 1.75f };
        public int[] carryCapacityLevels = { 1, 2, 2, 3 };
        [Tooltip("Hay units required to form one bale")]
        public int[] densityLevels = { 540, 495, 450, 405 };

        [Header("Economy")]
        public int purchaseCost;
        public int[] upgradeCosts = { 100, 250, 600 };

        [Header("Round Bale Physics (only used if isRoundBaler)")]
        public float mass = 80f;
        public float drag = 1.5f;
        public float angularDrag = 2.0f;
        [Tooltip("Slope angle (degrees) at which the bale starts rolling freely")]
        public float rollSlopeThreshold = 8f;
    }
}
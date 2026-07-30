using UnityEngine;

namespace Fields.Core.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Fields/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Grass Grid")]
        [Tooltip("Width/height of one CPU grid cell in metres (spec: 0.4)")]
        public float gridCellSize = 0.4f;

        [Header("Hay Collection")]
        [Tooltip("Side length of one hay collection cell in metres (spec: 6)")]
        public float collectionCellSize = 6f;
        [Tooltip("Uncut cells needed in one collection cell to spawn a HayPile (spec: 60)")]
        public int hayUnitsPerCollectionCell = 60;

        [Header("Hay Value Multipliers (by bale size)")]
        public float[] hayValueMultipliers = { 1f, 1.25f, 1.6f };

        [Header("Player Movement")]
        public float baseWalkSpeed = 4f;
        public float baseSprintSpeed = 6f;
        [Tooltip("Carry penalty at 1 / 2 / 3 bales (spec: -25% / -40% / -50%)")]
        public float[] baleCarrySpeedPenalties = { 0.25f, 0.40f, 0.50f };

        [Header("Stamina")]
        public float staminaRegen = 15f;
        public float staminaDrainPerSwing = 12f;
    }
}
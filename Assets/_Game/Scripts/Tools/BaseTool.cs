using Fields.Core.Data;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Base class for all tools. Manages equip/unequip and delegates to subclasses.
    /// Mirror NetworkBehaviour wiring deferred to P2.
    /// </summary>
    public abstract class BaseTool : MonoBehaviour
    {
        [Header("Data")]
        public ToolData toolData;

        [Header("Runtime upgrade level (0 = base)")]
        [Range(0, 3)]
        public int upgradeLevel;

        protected bool _isEquipped;

        public virtual void OnEquip()
        {
            _isEquipped = true;
            gameObject.SetActive(true);
        }

        public virtual void OnUnequip()
        {
            _isEquipped = false;
            gameObject.SetActive(false);
        }

        public abstract void OnUsePrimary(bool pressed);

        /// <summary>Short description shown in the HUD when this tool is equipped.</summary>
        public virtual string ToolTip => string.Empty;

        // Stat helpers — always read from SO, never cached
        public float CurrentPower =>
            toolData != null && toolData.powerLevels.Length > upgradeLevel
                ? toolData.powerLevels[upgradeLevel] : 1f;

        public float CurrentSpeed =>
            toolData != null && toolData.speedLevels.Length > upgradeLevel
                ? toolData.speedLevels[upgradeLevel] : 1f;

        public float CurrentEndurance =>
            toolData != null && toolData.enduranceLevels.Length > upgradeLevel
                ? toolData.enduranceLevels[upgradeLevel] : 100f;
    }
}
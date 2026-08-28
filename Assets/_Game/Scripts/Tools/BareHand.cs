using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Empty-hand slot — player holds nothing, no grass cutting occurs.
    /// </summary>
    public class BareHand : BaseTool
    {
        public override void OnUsePrimary(bool pressed) { }
        public override string ToolTip =>
            Fields.Core.LocalizationManager.Instance?.Get("tool.barehand.tooltip")
            ?? "Empty hand  —  Pick up bales [E] · Drop [G]";
    }
}
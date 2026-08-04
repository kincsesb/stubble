using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Empty-hand slot — player holds nothing, no grass cutting occurs.
    /// </summary>
    public class BareHand : BaseTool
    {
        public override void OnUsePrimary(bool pressed) { }
        public override string ToolTip => "Üres kéz  —  Bálák felvétele [E] · Lerakás [G]";
    }
}
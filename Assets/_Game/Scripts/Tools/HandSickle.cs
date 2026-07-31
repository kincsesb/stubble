using Fields.Core;
using Fields.Grass;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Hand Sickle — first melee tool. Calls CutCapsule during the Sweep phase
    /// every frame, tracing from the previous tip position to the current one.
    /// Cut radius and arc driven by ToolData.powerLevels[upgradeLevel].
    /// </summary>
    public class HandSickle : MeleeToolBase
    {
        [Header("Sickle Geometry")]
        [Tooltip("World-space transform at the sickle's blade tip")]
        public Transform bladeTip;
        [Tooltip("Base cut radius at upgrade level 0")]
        public float baseCutRadius = 0.25f;
        [Tooltip("Arc sweep angle in degrees (total)")]
        public float arcDegrees = 120f;

        GrassField _targetField;
        Vector3 _prevTipPos;
        bool _sweepActive;

        // Hit/whiff tracking for P1 feel system
        int _cellsCutThisSwing;

        // ------------------------------------------------------------------ //
        // Sweep callbacks
        // ------------------------------------------------------------------ //

        protected override void OnSweepBegin()
        {
            _sweepActive = true;
            _cellsCutThisSwing = 0;
            _prevTipPos = bladeTip != null ? bladeTip.position : transform.position;
            TryConsumeStamina(10f);
        }

        protected override void OnSweepTick(float normalizedTime)
        {
            if (!_sweepActive || bladeTip == null) return;

            Vector3 currentTip = bladeTip.position;
            float radius = baseCutRadius * CurrentPower;

            // Find the GrassField under the blade
            if (_targetField == null)
                _targetField = FindAnyGrassField(currentTip);

            if (_targetField != null)
            {
                // Subscribe to count cells cut this swing (for hit/whiff in P1)
                _targetField.OnCellCut += OnCellCutDuringSwing;
                _targetField.CutCapsule(_prevTipPos, currentTip, radius);
                _targetField.OnCellCut -= OnCellCutDuringSwing;
            }

            _prevTipPos = currentTip;
        }

        protected override void OnSweepEnd()
        {
            _sweepActive = false;
            // Classify + trigger Feel
            int cellsInArc = Mathf.Max(1, Mathf.RoundToInt(baseCutRadius * CurrentPower / 0.4f * 2f));
            var result = SwingResultCalculator.Classify(_cellsCutThisSwing, cellsInArc);
            _feelController?.TriggerSwingFeel(result);
            _targetField = null;
        }

        // ------------------------------------------------------------------ //

        void OnCellCutDuringSwing(int col, int row) => _cellsCutThisSwing++;

        GrassField FindAnyGrassField(Vector3 near)
        {
            var fields = FindObjectsByType<GrassField>(FindObjectsSortMode.None);
            // Prefer the field whose bounds actually contain the point
            foreach (var f in fields)
                if (f.ContainsWorldPoint(near)) return f;
            // Fallback: nearest origin
            GrassField best = null; float bestDist = float.MaxValue;
            foreach (var f in fields)
            {
                float d = (f.transform.position - near).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = f; }
            }
            return best;
        }

        // ------------------------------------------------------------------ //

        public override void OnUsePrimary(bool pressed)
        {
            if (pressed && CurrentPhase == SwingPhase.Idle)
                base.OnUsePrimary(pressed);
            else if (pressed)
                base.OnUsePrimary(pressed);
        }

        public int CellsCutLastSwing => _cellsCutThisSwing;
    }
}
using Fields.Grass;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Classifies each swing as Full Hit / Partial / Whiff / Obstacle (spec §8.1.3).
    /// Called by MeleeToolBase at sweep-end with the cells-cut count.
    /// </summary>
    public static class SwingResultCalculator
    {
        // Thresholds per spec §8.1.3
        const float FULL_HIT_THRESHOLD    = 0.60f;
        const float PARTIAL_HIT_THRESHOLD = 0.15f;

        public static SwingResult Classify(int cellsCut, int cellsInArc)
        {
            if (cellsInArc <= 0) return SwingResult.Whiff;
            float ratio = (float)cellsCut / cellsInArc;
            return ratio >= FULL_HIT_THRESHOLD    ? SwingResult.FullHit  :
                   ratio >= PARTIAL_HIT_THRESHOLD ? SwingResult.Partial  :
                                                    SwingResult.Whiff;
        }

        public static SwingResult ClassifyWithObstacleCheck(int cellsCut, int cellsInArc, bool hitObstacle)
        {
            if (hitObstacle) return SwingResult.Obstacle;
            return Classify(cellsCut, cellsInArc);
        }
    }

    public enum SwingResult { FullHit, Partial, Whiff, Obstacle }
}

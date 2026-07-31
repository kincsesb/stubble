using Fields.Core;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Fields.Feel
{
    /// <summary>
    /// Central feel dispatcher for all swing results.
    /// Uses Feel's MMF_Player for each swing outcome — configure feedbacks in the Inspector.
    /// Feedback chains per result (spec §8.1.3):
    ///   Full Hit  — MMF_RotationSpring 2°  + MMF_RotationShake  + haptic short pulse
    ///   Partial   — MMF_RotationSpring 1.2° + lighter shake
    ///   Whiff     — MMF_RotationSpring 0.6° only
    ///   Obstacle  — MMF_RotationSpring -3° (reversed) + MMF_PositionShake + strong haptic
    /// First 3 swings: intensity multiplied by firstCutIntensityBonus.
    /// </summary>
    public class SwingFeelController : MonoBehaviour
    {
        [Header("MMF_Players — configure feedbacks in Inspector")]
        public MMF_Player feedbackFullHit;
        public MMF_Player feedbackPartial;
        public MMF_Player feedbackWhiff;
        public MMF_Player feedbackObstacle;

        [Header("First-cut bonus (spec §8.12)")]
        [Tooltip("Intensity multiplier for the first 3 swings")]
        public float firstCutIntensityBonus = 1.5f;

        int _swingCount;

        // ------------------------------------------------------------------ //

        public void TriggerSwingFeel(SwingResult result)
        {
            _swingCount++;
            float intensity = _swingCount <= 3 ? firstCutIntensityBonus : 1f;

            switch (result)
            {
                case SwingResult.FullHit:
                    feedbackFullHit?.PlayFeedbacks(transform.position, intensity);
                    break;
                case SwingResult.Partial:
                    feedbackPartial?.PlayFeedbacks(transform.position, intensity);
                    break;
                case SwingResult.Whiff:
                    feedbackWhiff?.PlayFeedbacks(transform.position, 1f);
                    break;
                case SwingResult.Obstacle:
                    feedbackObstacle?.PlayFeedbacks(transform.position, intensity);
                    break;
            }
        }

        public void ResetSwingCount() => _swingCount = 0;
    }
}

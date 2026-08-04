using System.Collections;
using UnityEngine;

namespace Fields.Feel
{
    /// <summary>
    /// Attach to SquareBale and RoundBale prefabs.
    /// Plays a cartoon pop-in scale bounce when the bale first appears.
    /// </summary>
    public class BaleSpawnFeel : MonoBehaviour
    {
        [Tooltip("Total bounce duration in seconds")]
        public float duration = 0.50f;
        [Tooltip("Peak overshoot scale (1 = no overshoot)")]
        public float overshoot = 1.22f;
        [Tooltip("Fraction of duration spent growing to overshoot")]
        [Range(0.3f, 0.8f)] public float growFraction = 0.55f;

        void Start() => StartCoroutine(PopIn());

        IEnumerator PopIn()
        {
            transform.localScale = Vector3.zero;
            float elapsed = 0f;
            float growEnd = duration * growFraction;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float scale;
                if (elapsed <= growEnd)
                {
                    // Ease-out grow to overshoot
                    float n = elapsed / growEnd;
                    float eased = 1f - Mathf.Pow(1f - n, 2f);
                    scale = Mathf.Lerp(0f, overshoot, eased);
                }
                else
                {
                    // Settle back to 1
                    float n = (elapsed - growEnd) / (duration - growEnd);
                    float eased = 1f - Mathf.Pow(1f - n, 3f);
                    scale = Mathf.Lerp(overshoot, 1f, eased);
                }
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            transform.localScale = Vector3.one;
            Destroy(this); // self-remove — one-shot
        }
    }
}

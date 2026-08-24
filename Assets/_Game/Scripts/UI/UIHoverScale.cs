using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Fields.UI
{
    /// <summary>
    /// Attach to any Button or interactive UI element.
    /// Scales up on hover (with a subtle spring overshoot) and back on exit.
    /// Uses unscaled time so it works correctly in paused menus (timeScale=0).
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float duration   = 0.12f;

        Vector3   _baseScale;
        Coroutine _coroutine;

        void Awake() => _baseScale = transform.localScale;

        public void OnPointerEnter(PointerEventData _) => Animate(_baseScale * hoverScale);
        public void OnPointerExit (PointerEventData _) => Animate(_baseScale);

        void Animate(Vector3 target)
        {
            if (_coroutine != null) StopCoroutine(_coroutine);
            _coroutine = StartCoroutine(ScaleTo(target));
        }

        IEnumerator ScaleTo(Vector3 target)
        {
            Vector3 from = transform.localScale;
            float   t    = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                transform.localScale = Vector3.LerpUnclamped(from, target, EaseOutBack(t));
                yield return null;
            }
            transform.localScale = target;
        }

        // Slight spring overshoot — gives buttons a "alive" feel on hover
        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}

using Fields.Save;
using TMPro;
using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Non-intrusive autosave indicator.
    /// Subscribes to SaveSystem.OnSaveCompleted and briefly fades in, holds, then fades out.
    /// Attach to any persistent GO; the indicator panel is found via the serialized reference.
    ///
    /// Timing:  fade-in 0.25 s → hold 1.5 s → fade-out 0.8 s
    /// Re-trigger during fade-out restarts from full opacity.
    /// </summary>
    public class AutosaveIndicator : MonoBehaviour
    {
        [Header("UI refs")]
        public CanvasGroup  group;
        public TextMeshProUGUI label;

        [Header("Timing (seconds)")]
        public float fadeInDuration  = 0.25f;
        public float holdDuration    = 1.5f;
        public float fadeOutDuration = 0.8f;

        enum State { Hidden, FadeIn, Hold, FadeOut }
        State _state = State.Hidden;
        float _timer;

        // ------------------------------------------------------------------ //

        void OnEnable()  => SaveSystem.OnSaveCompleted += Show;
        void OnDisable() => SaveSystem.OnSaveCompleted -= Show;

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            if (_state == State.Hidden) return;

            _timer += Time.unscaledDeltaTime;

            switch (_state)
            {
                case State.FadeIn:
                    SetAlpha(_timer / fadeInDuration);
                    if (_timer >= fadeInDuration)
                    {
                        SetAlpha(1f);
                        _timer = 0f;
                        _state = State.Hold;
                    }
                    break;

                case State.Hold:
                    if (_timer >= holdDuration)
                    {
                        _timer = 0f;
                        _state = State.FadeOut;
                    }
                    break;

                case State.FadeOut:
                    SetAlpha(1f - _timer / fadeOutDuration);
                    if (_timer >= fadeOutDuration)
                    {
                        SetAlpha(0f);
                        _state = State.Hidden;
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ //

        public void Show()
        {
            // Restart from fade-in regardless of current state
            _timer = 0f;
            _state = State.FadeIn;
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            if (group != null) group.alpha = Mathf.Clamp01(a);
        }
    }
}
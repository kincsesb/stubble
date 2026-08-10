using System.Collections;
using Fields.Core;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Fields.Feel
{
    /// <summary>
    /// Central game-feel dispatcher. Subscribes to GameEvents and runs
    /// camera shake, freeze-frames, screen flashes and volume pulses.
    /// Assign cameraRoot in Inspector (the transform that the camera is
    /// a child of — usually CameraRoot on the Player).
    /// </summary>
    public class GameFeelController : MonoBehaviour
    {
        public static GameFeelController Instance { get; private set; }

        [Header("References")]
        public Transform cameraRoot;
        public Volume globalVolume;

        [Header("Bale Created")]
        public float baleShakeDuration   = 0.30f;
        public float baleShakeAmplitude  = 0.055f;
        public float baleShakeFrequency  = 28f;
        public float baleFreezeSeconds   = 0.055f;
        public Color baleFlashColor      = new Color(1f, 0.85f, 0.1f, 0.45f);
        public float baleFlashDuration   = 0.55f;
        public float baleBloomBoost      = 1.8f;   // added to bloom intensity
        public float baleBloomFallback   = 0.4f;   // seconds to fade bloom back

        [Header("Selling")]
        public float sellShakeDuration   = 0.18f;
        public float sellShakeAmplitude  = 0.025f;
        public float sellShakeFrequency  = 22f;
        public Color sellFlashColor      = new Color(0.1f, 1f, 0.35f, 0.35f);
        public float sellFlashDuration   = 0.40f;

        [Header("Grass cut (throttled)")]
        [Range(1, 8)] public int grassCutEveryN = 5;
        public float grassShakeDuration  = 0.08f;
        public float grassShakeAmplitude = 0.012f;
        public float grassShakeFrequency = 35f;

        [Header("Feel — Heavy Bale Impact")]
        [Tooltip("MMF_Player: Cinemachine Impulse + Chromatic Aberration + rumble. Plays on heavy bale collision.")]
        public MMF_Player feedbackBaleImpact;
        public float baleImpactSpeedThreshold = 5f;   // m/s — below this, no impact feel

        [Header("Feel — Achievement Unlocked")]
        [Tooltip("MMF_Player: big Bloom + Flash + FreezeFrame. Play via TriggerAchievementFeel().")]
        public MMF_Player feedbackAchievement;

        [Header("Feel — Terminal Open / Close")]
        [Tooltip("MMF_Player: brief Chromatic Aberration glitch when the cheat/AI terminal opens.")]
        public MMF_Player feedbackTerminalOpen;

        [Header("Feel — Big Sale")]
        [Tooltip("MMF_Player: ColorAdjustments saturation spike. Plays when a single sale exceeds bigSaleThreshold.")]
        public MMF_Player feedbackBigSale;
        public int bigSaleThreshold = 500;

        // Runtime state
        Image       _flashImage;
        Bloom       _bloom;
        float       _bloomBase;
        int         _cutCount;

        Coroutine   _shakeRoutine;
        Coroutine   _flashRoutine;
        Coroutine   _bloomRoutine;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildFlashImage();
            CacheBloom();
        }

        void OnEnable()
        {
            GameEvents.OnBaleCreated  += HandleBaleCreated;
            GameEvents.OnBaleSold     += HandleBaleSold;
            GameEvents.OnGridCellCut  += HandleGrassCut;
        }

        void OnDisable()
        {
            GameEvents.OnBaleCreated  -= HandleBaleCreated;
            GameEvents.OnBaleSold     -= HandleBaleSold;
            GameEvents.OnGridCellCut  -= HandleGrassCut;
        }

        // ------------------------------------------------------------------ //
        // Event handlers
        // ------------------------------------------------------------------ //

        void HandleBaleCreated(int parcelId, int playerId, bool isRound)
        {
            StartShake(baleShakeDuration, baleShakeAmplitude, baleShakeFrequency);
            StartFreezeFrame(baleFreezeSeconds);
            PulseBloom(baleBloomBoost, baleBloomFallback);
        }

        void HandleBaleSold(int parcelId, int playerId, int value)
        {
            float intensity = Mathf.Clamp01(value / 300f) * 0.5f + 0.5f;
            StartShake(sellShakeDuration, sellShakeAmplitude * intensity, sellShakeFrequency);
            StartFlash(sellFlashColor * intensity + sellFlashColor * (1f - intensity), sellFlashDuration);
            if (value >= bigSaleThreshold)
                feedbackBigSale?.PlayFeedbacks(transform.position, intensity);
        }

        void HandleGrassCut(int parcelId, int playerId, float area)
        {
            if (++_cutCount % grassCutEveryN != 0) return;
            StartShake(grassShakeDuration, grassShakeAmplitude, grassShakeFrequency);
        }

        // ------------------------------------------------------------------ //
        // External triggers (called from other systems)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Heavy bale roll/collision impact. Pass the relative speed at impact.
        /// </summary>
        public void TriggerBaleImpact(float relativeSpeed)
        {
            if (relativeSpeed < baleImpactSpeedThreshold) return;
            float t = Mathf.InverseLerp(baleImpactSpeedThreshold, baleImpactSpeedThreshold * 3f, relativeSpeed);
            feedbackBaleImpact?.PlayFeedbacks(transform.position, Mathf.Lerp(0.5f, 1.5f, t));
        }

        /// <summary>Achievement popup feel — celebratory burst.</summary>
        public void TriggerAchievementFeel()
        {
            feedbackAchievement?.PlayFeedbacks(transform.position, 1f);
        }

        /// <summary>Terminal open/close CRT-glitch feel.</summary>
        public void TriggerTerminalFeel()
        {
            feedbackTerminalOpen?.PlayFeedbacks(transform.position, 1f);
        }

        // ------------------------------------------------------------------ //
        // Camera shake
        // ------------------------------------------------------------------ //

        public void StartShake(float duration, float amplitude, float frequency)
        {
            if (cameraRoot == null) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
        }

        IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
        {
            Vector3 origin = cameraRoot.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - elapsed / duration;
                float t = elapsed * frequency * Mathf.PI * 2f;
                float x = Mathf.Sin(t)       * amplitude * decay;
                float y = Mathf.Sin(t * 1.3f) * amplitude * decay * 0.6f;
                cameraRoot.localPosition = origin + new Vector3(x, y, 0f);
                yield return null;
            }
            cameraRoot.localPosition = origin;
        }

        // ------------------------------------------------------------------ //
        // Freeze frame
        // ------------------------------------------------------------------ //

        void StartFreezeFrame(float seconds)
        {
            if (seconds > 0f) StartCoroutine(FreezeRoutine(seconds));
        }

        IEnumerator FreezeRoutine(float seconds)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ //
        // Screen flash
        // ------------------------------------------------------------------ //

        void BuildFlashImage()
        {
            // Create a Canvas overlay with a full-screen Image
            var canvasGO = new GameObject("GameFeel_FlashCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGO.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("FlashImage");
            imgGO.transform.SetParent(canvasGO.transform, false);
            var rt = imgGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _flashImage = imgGO.AddComponent<Image>();
            _flashImage.color = Color.clear;
            _flashImage.raycastTarget = false;
        }

        void StartFlash(Color color, float duration)
        {
            if (_flashImage == null) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            _flashImage.color = color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                _flashImage.color = Color.Lerp(color, Color.clear, t * t);
                yield return null;
            }
            _flashImage.color = Color.clear;
        }

        // ------------------------------------------------------------------ //
        // Bloom pulse (bale completion)
        // ------------------------------------------------------------------ //

        void CacheBloom()
        {
            if (globalVolume == null) return;
            if (globalVolume.profile.TryGet<Bloom>(out _bloom))
                _bloomBase = _bloom.intensity.value;
        }

        void PulseBloom(float boost, float fallbackSeconds)
        {
            if (_bloom == null) return;
            if (_bloomRoutine != null) StopCoroutine(_bloomRoutine);
            _bloomRoutine = StartCoroutine(BloomPulse(boost, fallbackSeconds));
        }

        IEnumerator BloomPulse(float boost, float fallbackSeconds)
        {
            _bloom.intensity.Override(_bloomBase + boost);
            float elapsed = 0f;
            while (elapsed < fallbackSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fallbackSeconds;
                _bloom.intensity.Override(Mathf.Lerp(_bloomBase + boost, _bloomBase, t));
                yield return null;
            }
            _bloom.intensity.Override(_bloomBase);
        }
    }
}

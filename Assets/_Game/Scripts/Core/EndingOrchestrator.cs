using System.Collections;
using Fields.Grass;
using Fields.Save;
using Fields.Settings;
using Fields.Tools;
using Fields.UI;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fields.Core
{
    /// <summary>
    /// Routes game completion to one of three endings based on
    /// SettingsData.theatricalEnding and total playtime.
    ///
    ///   theatrical ON  + < 3 h  → Speed-Run Loop  (grass resets, scene restarts)
    ///   theatrical ON  + ≥ 3 h  → Nuclear         (plane, bomb, explosion)
    ///   theatrical OFF          → Peaceful         (EndScreen immediately)
    ///
    /// Place on any scene GameObject. Wire all Inspector fields before play.
    /// </summary>
    public class EndingOrchestrator : MonoBehaviour
    {
        public static EndingOrchestrator Instance { get; private set; }

        const float SPEEDRUN_THRESHOLD_SECONDS = 10800f; // 3 hours

        // ── Scene references ──────────────────────────────────────────────── //

        [Header("Scene References")]
        [Tooltip("All GrassField components in the scene.")]
        public GrassField[] grassFields;

        [Tooltip("The airplane_3d_model Transform already placed in the scene.")]
        public Transform airplaneTransform;

        [Tooltip("The 'Bomb' empty GameObject — airplane flies toward this.")]
        public Transform bombTarget;

        [Tooltip("Prefab instantiated and dropped when the plane passes over.")]
        public GameObject nuclearBombPrefab;

        [Tooltip("vfx_StylizedExplosion_Nuke_L ParticleSystem in the scene.")]
        public ParticleSystem vfxNuke;

        [Tooltip("Separate URP Volume (priority 20, weight 0). Profile needs Bloom/ChromAb/Vignette/ColorAdj/LensDistort.")]
        public Volume nuclearVolume;

        [Tooltip("The endScreenRoot GameObject (same one WorldBootstrap references).")]
        public GameObject endScreenRoot;

        // ── Letterbox ─────────────────────────────────────────────────────── //

        [Header("Letterbox")]
        [Tooltip("Black bar RectTransform anchored to top of screen.")]
        public RectTransform letterboxTop;

        [Tooltip("Black bar RectTransform anchored to bottom of screen.")]
        public RectTransform letterboxBottom;

        [Tooltip("Height in pixels of each letterbox bar.")]
        public float letterboxHeight = 80f;

        public float letterboxDuration = 0.45f;

        // ── Loop title card ───────────────────────────────────────────────── //

        [Header("Loop Title Card")]
        [Tooltip("CanvasGroup wrapping the 'Fields Refreshed!' overlay. Starts disabled.")]
        public CanvasGroup loopTitleCard;

        [Tooltip("TMP text inside the loop title card.")]
        public TextMeshProUGUI loopTitleText;

        // ── Audio ─────────────────────────────────────────────────────────── //

        [Header("Audio")]
        public AudioSource airplaneAudioSource;
        public AudioSource explosionAudioSource;
        public AudioClip airplaneClip;    // Assets/SFX/airplane.mp3
        public AudioClip nuclearBombClip; // Assets/SFX/nuclear-bomb.mp3

        // ── Feel ──────────────────────────────────────────────────────────── //

        [Header("Feel — Shared")]
        [Tooltip("MMF_Player: letterbox + vignette + subtle time-scale dip.")]
        public MMF_Player feedbackCinematicIn;

        [Header("Feel — Loop Ending")]
        [Tooltip("MMF_Player: PositionShake + Bloom + ChromAb + FOV punch + Flash + FreezeFrame.")]
        public MMF_Player feedbackGrassReturn;

        [Header("Feel — Nuclear Ending")]
        [Tooltip("MMF_Player: progressive shake + chromatic aberration build-up.")]
        public MMF_Player feedbackNuclearBuild;

        [Tooltip("MMF_Player: Flash white + mega PositionShake + RotationShake + Bloom + ChromAb + FOV punch + FreezeFrame + Rumble.")]
        public MMF_Player feedbackNuclearBlast;

        [Tooltip("MMF_Player: slow settle shake + vignette ease-out.")]
        public MMF_Player feedbackNuclearSettle;

        // ── Timing ────────────────────────────────────────────────────────── //

        [Header("Timing")]
        public float playerTurnDuration  = 2.0f;
        public float airplaneTravelTime  = 9.0f;
        public float bombDropDuration    = 1.8f;
        public float postBlastHold       = 4.0f;
        public float fadeOutDuration     = 1.5f;

        // ── Nuclear post-process ──────────────────────────────────────────── //

        [Header("Nuclear Post-Process")]
        public float nukeBloomPeak       = 12f;
        public float nukeChromAbPeak     = 1f;
        public float nukeVignettePeak    = 0.75f;
        public float nukeSaturationPeak  = 60f;
        public float nukeHueShift        = -15f;
        public float nukeLensDistort     = -0.3f;
        public float nukeLightIntensity  = 200f;
        public float nukeLightRange      = 150f;
        public Color nukeLightColor      = new Color(1f, 0.6f, 0.1f);
        public float nukePostDecayTime   = 3.0f;

        // ── Runtime ───────────────────────────────────────────────────────── //

        Bloom             _nukeBloom;
        ChromaticAberration _nukeChromAb;
        Vignette          _nukeVignette;
        ColorAdjustments  _nukeColorAdj;
        LensDistortion    _nukeLensDistort;
        Image             _fadeImage;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildFadeOverlay();
            CacheNuclearVolume();
        }

        void OnEnable()  => GameEvents.OnFullGameCompleted += HandleGameCompleted;
        void OnDisable() => GameEvents.OnFullGameCompleted -= HandleGameCompleted;

        // ======================================================================
        // Routing
        // ======================================================================

        void HandleGameCompleted(float totalPlaytimeSeconds)
        {
            var sm = SettingsManager.Instance;
            bool theatrical = sm == null || sm.Data.theatricalEnding;

            if (!theatrical)
            {
                EndScreen.PendingEndingType = EndScreen.EndingType.Peaceful;
                if (endScreenRoot != null) endScreenRoot.SetActive(true);
                return;
            }

            if (totalPlaytimeSeconds < SPEEDRUN_THRESHOLD_SECONDS)
                StartCoroutine(SpeedRunLoopSequence());
            else
                StartCoroutine(NuclearSequence());
        }

        // ======================================================================
        // Speed-Run Loop Sequence  (< 3 hours)
        // ======================================================================

        IEnumerator SpeedRunLoopSequence()
        {
            EndScreen.PendingEndingType = EndScreen.EndingType.Loop;
            SetPlayerInput(false);

            // Bare hands — drop whatever tool was held
            var pc = PlayerController.Instance;
            if (pc != null)
                pc.GetComponentInChildren<ToolHolder>()?.EquipBareHand();

            feedbackCinematicIn?.PlayFeedbacks();
            yield return StartCoroutine(AnimateLetterbox(enter: true));
            yield return StartCoroutine(RotatePlayersToward(GetFieldCenter(), playerTurnDuration));

            yield return new WaitForSecondsRealtime(0.5f);

            // BUMP — theatrical wave pop-in: grass ripples outward from field center
            feedbackGrassReturn?.PlayFeedbacks();
            Vector3 center = GetFieldCenter();
            foreach (var gf in grassFields)
            {
                if (gf == null) continue;
                gf.ResetGrass();
                gf.GetComponent<GrassChunkManager>()?.AnimatePopInWave(center);
            }

            // Wait for wave to finish (maxDelay + duration + small buffer)
            yield return new WaitForSecondsRealtime(1.5f);

            yield return StartCoroutine(ShowLoopTitleCard());

            // Reset parcel completion so the field can be won again
            WorldBootstrap.Instance?.ResetAllParcels();

            // Persist the fresh grass state
            SaveSystem.Instance?.SaveGame();

            // Exit cinematic — letterboxes slide back out
            yield return StartCoroutine(AnimateLetterbox(enter: false));

            // Return control — player continues from exactly where they are
            SetPlayerInput(true);
        }

        // ======================================================================
        // Nuclear Sequence  (≥ 3 hours)
        // ======================================================================

        IEnumerator NuclearSequence()
        {
            EndScreen.PendingEndingType = EndScreen.EndingType.Nuclear;
            SetPlayerInput(false);

            feedbackCinematicIn?.PlayFeedbacks();
            yield return StartCoroutine(AnimateLetterbox(enter: true));

            // Players turn to face the airplane
            Vector3 faceTarget = airplaneTransform != null
                ? airplaneTransform.position
                : transform.position + Vector3.forward * 50f;
            yield return StartCoroutine(RotatePlayersToward(faceTarget, playerTurnDuration));

            // Start airplane audio (fades in during travel)
            if (airplaneAudioSource != null && airplaneClip != null)
            {
                airplaneAudioSource.clip  = airplaneClip;
                airplaneAudioSource.loop  = true;
                airplaneAudioSource.volume = 0f;
                airplaneAudioSource.Play();
            }

            feedbackNuclearBuild?.PlayFeedbacks();

            // Fly airplane toward the Bomb target
            Vector3 bombPos = bombTarget != null ? bombTarget.position : Vector3.zero;
            yield return StartCoroutine(MoveAirplane(bombPos, airplaneTravelTime));

            // Drop the bomb
            GameObject bomb = null;
            if (nuclearBombPrefab != null && airplaneTransform != null)
                bomb = Instantiate(nuclearBombPrefab, airplaneTransform.position, Quaternion.identity);

            yield return StartCoroutine(DropBomb(bomb, bombPos, bombDropDuration));

            // EXPLOSION
            if (airplaneAudioSource != null) airplaneAudioSource.Stop();

            if (explosionAudioSource != null && nuclearBombClip != null)
                explosionAudioSource.PlayOneShot(nuclearBombClip, 1f);

            if (vfxNuke != null) vfxNuke.Play();

            feedbackNuclearBlast?.PlayFeedbacks();
            StartCoroutine(AnimateNuclearVolume());
            StartCoroutine(SpawnNukeLight(bombPos));

            yield return new WaitForSecondsRealtime(postBlastHold);

            feedbackNuclearSettle?.PlayFeedbacks();
            SteamManager.Instance?.UnlockAchievement(SteamManager.Achievements.THE_HARD_WAY);

            yield return new WaitForSecondsRealtime(1.5f);
            yield return StartCoroutine(FadeToBlack(fadeOutDuration));

            if (endScreenRoot != null) endScreenRoot.SetActive(true);
        }

        // ======================================================================
        // Airplane movement
        // ======================================================================

        IEnumerator MoveAirplane(Vector3 target, float duration)
        {
            if (airplaneTransform == null) yield break;

            Vector3 start       = airplaneTransform.position;
            Vector3 targetFlat  = new Vector3(target.x, start.y, target.z);
            Quaternion startRot = airplaneTransform.rotation;
            Vector3 dir         = targetFlat - start;
            Quaternion targetRot = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir.normalized)
                : startRot;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                airplaneTransform.position = Vector3.Lerp(start, targetFlat, t);
                // Quick initial rotation snap, then hold
                airplaneTransform.rotation = Quaternion.Slerp(startRot, targetRot, Mathf.Min(t * 4f, 1f));

                // Fade in audio as plane gets closer
                if (airplaneAudioSource != null)
                    airplaneAudioSource.volume = Mathf.Clamp01(t * 2f);

                yield return null;
            }

            airplaneTransform.position = targetFlat;
        }

        // ======================================================================
        // Bomb drop
        // ======================================================================

        IEnumerator DropBomb(GameObject bomb, Vector3 target, float duration)
        {
            if (bomb == null) yield break;

            // Take control away from any Rigidbody on the prefab
            var rb = bomb.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Vector3 start      = bomb.transform.position;
            Vector3 dropTarget = GrassField.SnapToTerrain(target);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Ease-in squared gives gravity feel
                bomb.transform.position = Vector3.Lerp(start, dropTarget, t * t);
                yield return null;
            }

            bomb.transform.position = dropTarget;
        }

        // ======================================================================
        // Player rotation
        // ======================================================================

        IEnumerator RotatePlayersToward(Vector3 worldTarget, float duration)
        {
            var pc = PlayerController.Instance;
            if (pc == null) yield break;

            Vector3 dir = worldTarget - pc.transform.position;
            dir.y = 0f;
            Quaternion targetRot = dir.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(dir.normalized)
                : pc.transform.rotation;

            Quaternion startRot = pc.transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                pc.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            pc.transform.rotation = targetRot;
        }

        // ======================================================================
        // Letterbox animation
        // ======================================================================

        IEnumerator AnimateLetterbox(bool enter)
        {
            if (letterboxTop == null || letterboxBottom == null) yield break;

            // Top bar: hidden at +letterboxHeight, visible at 0
            // Bottom bar: hidden at -letterboxHeight, visible at 0
            float fromTop = enter ? letterboxHeight  :  0f;
            float toTop   = enter ? 0f               :  letterboxHeight;
            float fromBot = enter ? -letterboxHeight  : 0f;
            float toBot   = enter ? 0f               : -letterboxHeight;

            float elapsed = 0f;
            while (elapsed < letterboxDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / letterboxDuration);
                letterboxTop.anchoredPosition    = new Vector2(0f, Mathf.Lerp(fromTop, toTop, t));
                letterboxBottom.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromBot, toBot, t));
                yield return null;
            }

            letterboxTop.anchoredPosition    = new Vector2(0f, toTop);
            letterboxBottom.anchoredPosition = new Vector2(0f, toBot);
        }

        // ======================================================================
        // Loop title card
        // ======================================================================

        IEnumerator ShowLoopTitleCard()
        {
            if (loopTitleCard == null) yield break;

            var loc = LocalizationManager.Instance;
            if (loopTitleText != null)
                loopTitleText.text = loc != null ? loc.Get("end.loop.title") : "Fields Refreshed!";

            loopTitleCard.gameObject.SetActive(true);
            loopTitleCard.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                loopTitleCard.alpha = elapsed / 0.5f;
                yield return null;
            }
            loopTitleCard.alpha = 1f;

            yield return new WaitForSecondsRealtime(1.8f);

            elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.unscaledDeltaTime;
                loopTitleCard.alpha = 1f - elapsed / 0.6f;
                yield return null;
            }
            loopTitleCard.gameObject.SetActive(false);
        }

        // ======================================================================
        // Fade to black
        // ======================================================================

        IEnumerator FadeToBlack(float duration)
        {
            if (_fadeImage == null) yield break;

            _fadeImage.gameObject.SetActive(true);
            Color c = Color.black;
            c.a = 0f;
            _fadeImage.color = c;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                c.a = Mathf.Clamp01(elapsed / duration);
                _fadeImage.color = c;
                yield return null;
            }
            _fadeImage.color = Color.black;
        }

        // ======================================================================
        // Nuclear post-process
        // ======================================================================

        IEnumerator AnimateNuclearVolume()
        {
            if (nuclearVolume == null) yield break;

            // Hard peak
            nuclearVolume.weight = 1f;
            if (_nukeBloom    != null) _nukeBloom.intensity.Override(nukeBloomPeak);
            if (_nukeChromAb  != null) _nukeChromAb.intensity.Override(nukeChromAbPeak);
            if (_nukeVignette != null) _nukeVignette.intensity.Override(nukeVignettePeak);
            if (_nukeColorAdj != null)
            {
                _nukeColorAdj.saturation.Override(nukeSaturationPeak);
                _nukeColorAdj.hueShift.Override(nukeHueShift);
            }
            if (_nukeLensDistort != null) _nukeLensDistort.intensity.Override(nukeLensDistort);

            // Brief hold at peak (matches flash white-out)
            yield return new WaitForSecondsRealtime(0.15f);

            // Decay
            float elapsed = 0f;
            while (elapsed < nukePostDecayTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / nukePostDecayTime;
                nuclearVolume.weight = Mathf.Lerp(1f, 0f, t * t);
                yield return null;
            }

            nuclearVolume.weight = 0f;
        }

        IEnumerator SpawnNukeLight(Vector3 pos)
        {
            var lightGO = new GameObject("NukeExplosionLight");
            lightGO.transform.position = pos + Vector3.up * 5f;
            var light = lightGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = nukeLightColor;
            light.range     = nukeLightRange;
            light.intensity = nukeLightIntensity;

            float elapsed = 0f;
            const float duration = 2.0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                light.intensity = Mathf.Lerp(nukeLightIntensity, 0f, elapsed / duration);
                yield return null;
            }

            Destroy(lightGO);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        Vector3 GetFieldCenter()
        {
            if (grassFields == null || grassFields.Length == 0)
                return new Vector3(100f, 0f, 100f);

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var gf in grassFields)
            {
                if (gf == null) continue;
                // transform.position is the bottom-left origin; add half-size to get center
                Vector3 center = gf.transform.position + new Vector3(gf.fieldSize.x * 0.5f, 0f, gf.fieldSize.y * 0.5f);
                sum += center;
                count++;
            }
            return count > 0 ? sum / count : new Vector3(100f, 0f, 100f);
        }

        static void SetPlayerInput(bool enabled)
        {
            var pc = PlayerController.Instance;
            if (pc != null) pc.InputLocked = !enabled;
        }

        void BuildFadeOverlay()
        {
            var canvasGO = new GameObject("EndingFadeCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 998;
            canvasGO.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("FadeImage");
            imgGO.transform.SetParent(canvasGO.transform, false);
            var rt = imgGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _fadeImage = imgGO.AddComponent<Image>();
            _fadeImage.color         = Color.clear;
            _fadeImage.raycastTarget = false;
            imgGO.SetActive(false);
        }

        void CacheNuclearVolume()
        {
            if (nuclearVolume == null) return;
            nuclearVolume.weight = 0f;
            var p = nuclearVolume.profile;
            if (p == null) return;
            p.TryGet(out _nukeBloom);
            p.TryGet(out _nukeChromAb);
            p.TryGet(out _nukeVignette);
            p.TryGet(out _nukeColorAdj);
            p.TryGet(out _nukeLensDistort);
        }
    }
}

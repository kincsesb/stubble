using System.Collections;
using Fields.Grass;
using Fields.Hay;
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
    ///   theatrical ON  + < 3 h  → Speed-Run Loop  (grass resets, player continues)
    ///   theatrical ON  + ≥ 3 h  → Nuclear         (plane, bomb, explosion)
    ///   theatrical OFF          → Peaceful         (EndScreen immediately)
    ///
    /// Place on any scene GameObject. Wire all Inspector fields before play.
    /// </summary>
    public class EndingOrchestrator : MonoBehaviour
    {
        public static EndingOrchestrator Instance { get; private set; }

        const float SPEEDRUN_THRESHOLD_SECONDS = 10800f; // 3 hours

        // Fixed world-space rotation kept on the airplane model throughout the sequence.
        static readonly Quaternion AIRPLANE_FIXED_ROTATION = Quaternion.Euler(-90f, 0f, 54f);

        // ── Scene references ──────────────────────────────────────────────── //

        [Header("Scene References")]
        [Tooltip("All GrassField components in the scene.")]
        public GrassField[] grassFields;

        [Tooltip("The airplane_3d_model Transform already placed in the scene.")]
        public Transform airplaneTransform;

        [Tooltip("The 'Bomb' empty GameObject — airplane flies toward this, bomb spawns here.")]
        public Transform bombTarget;

        [Tooltip("Prefab instantiated and dropped when the plane passes over.")]
        public GameObject nuclearBombPrefab;

        [Tooltip("vfx_StylizedExplosion_Nuke_L ParticleSystem already placed in the scene.")]
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
        public AudioClip airplaneClip;
        public AudioClip nuclearBombClip;

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
        public float bombDropDuration    = 4.0f;
        public float postBlastHold       = 2.5f;
        public float fadeOutDuration     = 1.5f;

        [Tooltip("Slerp speed for player body/pitch tracking during the cinematic.")]
        public float playerTrackSpeed    = 3.5f;

        [Tooltip("Time.timeScale applied the moment the nuke VFX plays (0.7 = 30% slowdown). Restored before fade-out.")]
        public float nuclearSlowMoScale  = 0.7f;

        [Tooltip("Additional simulationSpeed multiplier on the nuke VFX particle system (0.3 = 70% further slowdown on top of timeScale).")]
        public float nuclearVfxSimSpeed  = 0.3f;

        // ── Nuclear Camera ────────────────────────────────────────────────── //

        [Header("Nuclear Camera")]
        [Tooltip("FOV during airplane tracking (narrower = more zoom).")]
        public float fovAirplane = 48f;

        [Tooltip("FOV during bomb drop (extra zoom on the falling bomb).")]
        public float fovBomb = 38f;

        [Tooltip("Seconds to reach each FOV target.")]
        public float fovZoomDuration = 2.0f;

        [Tooltip("World-space offset of the dedicated bomb camera from the bomb position. Keep Y=0 for a perfectly horizontal side view.")]
        public Vector3 bombCamOffset = new Vector3(10f, 0f, 0f);

        [Tooltip("Seconds before the bomb hits terrain to cut back to the player camera (so the explosion is seen from the player's perspective).")]
        public float preImpactCamRestoreTime = 1.5f;

        [Tooltip("Downward pitch in degrees applied to the player camera the moment it cuts back from the bomb cam (so the player looks toward the impact).")]
        public float preImpactCamPitchDown = 25f;

        // ── Nuclear Shockwave ─────────────────────────────────────────────── //

        [Header("Nuclear Shockwave")]
        [Tooltip("Prefab (flat disc/ring mesh with fade material) spawned at impact and scaled outward.")]
        public GameObject shockwavePrefab;

        [Tooltip("Max world-space scale of the shockwave ring.")]
        public float shockwaveMaxScale = 100f;

        [Tooltip("Time in seconds for the shockwave to expand fully.")]
        public float shockwaveDuration = 2.5f;

        // ── Nuclear post-process ──────────────────────────────────────────── //

        [Header("Nuclear Post-Process")]
        public float nukeBloomPeak       = 12f;
        public float nukeChromAbPeak     = 1f;
        public float nukeVignettePeak    = 0.75f;
        public float nukeSaturationPeak  = 60f;
        public float nukeHueShift        = -15f;
        public float nukeLensDistort     = -0.3f;

        [Tooltip("Instantaneous white-flash intensity at the moment of detonation.")]
        public float nukeFlashIntensity  = 15000f;

        [Tooltip("Sustained orange fire glow intensity after the flash fades.")]
        public float nukeLightIntensity  = 4000f;

        [Tooltip("Point light range for the explosion glow.")]
        public float nukeLightRange      = 300f;

        public Color nukeLightColor      = new Color(1f, 0.6f, 0.1f);
        public float nukePostDecayTime   = 4.0f;

        [Header("Blinding Flash")]
        [Tooltip("Seconds the screen stays fully white before it begins fading.")]
        public float flashHoldDuration   = 0.08f;
        [Tooltip("Seconds for the white screen to fade completely to clear.")]
        public float flashFadeDuration   = 1.8f;

        // ── Runtime ───────────────────────────────────────────────────────── //

        Bloom               _nukeBloom;
        ChromaticAberration _nukeChromAb;
        Vignette            _nukeVignette;
        ColorAdjustments    _nukeColorAdj;
        LensDistortion      _nukeLensDistort;
        Image               _fadeImage;

        // Stored travel direction so ContinueAirplane doesn't rely on airplaneTransform.forward
        Vector3 _airplaneTravelDir;

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
                StartCoroutine(PeacefulReloadSequence());
                return;
            }

            if (totalPlaytimeSeconds < SPEEDRUN_THRESHOLD_SECONDS)
                StartCoroutine(SpeedRunLoopSequence());
            else
                StartCoroutine(NuclearSequence());
        }

        // ======================================================================
        // Peaceful Reload Sequence  (theatrical OFF)
        // ======================================================================

        IEnumerator PeacefulReloadSequence()
        {
            SetPlayerInput(false);
            yield return StartCoroutine(FadeToBlack(fadeOutDuration));
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
            // Disable Flash feedbacks for the loop ending — only the bump/shake is wanted here.
            var flashFeedbacks = new System.Collections.Generic.List<MMF_Feedback>();
            if (feedbackGrassReturn != null)
                foreach (var fb in feedbackGrassReturn.FeedbacksList)
                    if (fb is MMF_Flash) { flashFeedbacks.Add(fb); fb.Active = false; }
            feedbackGrassReturn?.PlayFeedbacks();
            foreach (var fb in flashFeedbacks) fb.Active = true;
            Vector3 center = GetFieldCenter();
            foreach (var gf in grassFields)
            {
                if (gf == null) continue;
                gf.ResetGrass();
                gf.GetComponent<GrassChunkManager>()?.AnimatePopInWave(center);
                // Clear accumulated hay and destroy any uncollected hay piles so the player
                // must cut the regrown grass before baling again.
                gf.GetComponent<HayAccumulationSystem>()?.ResetWithPiles();
            }

            yield return new WaitForSecondsRealtime(1.5f);

            WorldBootstrap.Instance?.ResetAllParcels();
            SaveSystem.Instance?.SaveGame();

            yield return StartCoroutine(AnimateLetterbox(enter: false));
            SetPlayerInput(true);
        }

        // ======================================================================
        // Nuclear Sequence  (≥ 3 hours)
        // ======================================================================

        IEnumerator NuclearSequence()
        {
            EndScreen.PendingEndingType = EndScreen.EndingType.Nuclear;
            SetPlayerInput(false);

            // Suppress PlayerController's own FOV/bob logic during the cinematic
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.IsMounted = true;
                pc.GetComponentInChildren<ToolHolder>()?.EquipBareHand();
            }

            try { feedbackCinematicIn?.PlayFeedbacks(); } catch (System.Exception e) { Debug.LogWarning($"[Ending] feedbackCinematicIn failed: {e.Message}"); }
            yield return StartCoroutine(AnimateLetterbox(enter: true));

            // Initial turn: face the airplane
            Vector3 initialFace = airplaneTransform != null
                ? airplaneTransform.position
                : transform.position + Vector3.forward * 50f;
            yield return StartCoroutine(RotatePlayersToward(initialFace, playerTurnDuration));

            // Start airplane audio
            if (airplaneAudioSource != null && airplaneClip != null)
            {
                airplaneAudioSource.clip   = airplaneClip;
                airplaneAudioSource.loop   = true;
                airplaneAudioSource.volume = 0f;
                airplaneAudioSource.Play();
            }

            try { feedbackNuclearBuild?.PlayFeedbacks(); } catch (System.Exception e) { Debug.LogWarning($"[Ending] feedbackNuclearBuild failed: {e.Message}"); }

            // Compute and store travel direction for ContinueAirplane
            Vector3 bombPos = bombTarget != null ? bombTarget.position : Vector3.zero;
            if (airplaneTransform != null)
            {
                _airplaneTravelDir = bombPos - airplaneTransform.position;
                _airplaneTravelDir.y = 0f;
                if (_airplaneTravelDir.sqrMagnitude > 0.001f)
                    _airplaneTravelDir.Normalize();
            }

            float airplaneSpeed = airplaneTransform != null
                ? Vector3.Distance(new Vector3(airplaneTransform.position.x, 0f, airplaneTransform.position.z),
                                   new Vector3(bombPos.x, 0f, bombPos.z))
                  / Mathf.Max(airplaneTravelTime, 0.1f)
                : 20f;

            // Zoom in on airplane + continuously track it
            StartCoroutine(AnimateFOV(fovAirplane, fovZoomDuration));
            var planeTrack = StartCoroutine(TrackWithPlayerCamera(airplaneTransform));
            yield return StartCoroutine(MoveAirplane(bombPos, airplaneTravelTime));
            StopCoroutine(planeTrack);

            // Spawn bomb nose-down (-90° on X = tip pointing toward ground)
            GameObject bomb = null;
            if (nuclearBombPrefab != null && airplaneTransform != null)
                bomb = Instantiate(nuclearBombPrefab, airplaneTransform.position, Quaternion.Euler(-90f, 0f, 0f));

            // Airplane keeps flying forward (fire-and-forget)
            StartCoroutine(ContinueAirplane(airplaneSpeed, airplaneTravelTime * 0.5f));

            // Switch to a dedicated bomb-follow camera for the drop
            Vector3 dropOrigin = bomb != null ? bomb.transform.position : bombPos;
            Vector3 impactPos  = ComputeGroundPosition(dropOrigin);

            if (bomb != null)
                StartCoroutine(RunBombCamera(bomb, bombDropDuration));

            // Slow-motion begins the moment the bomb starts falling
            Time.timeScale = nuclearSlowMoScale;

            // Drop bomb straight down to terrain (uses unscaledDeltaTime — unaffected by timeScale)
            if (bomb != null)
                yield return StartCoroutine(DropBombDown(bomb, impactPos));
            else
                yield return new WaitForSecondsRealtime(bombDropDuration);

            // EXPLOSION ─────────────────────────────────────────────────────
            if (airplaneAudioSource != null) { airplaneAudioSource.Stop(); airplaneAudioSource.loop = false; }

            if (explosionAudioSource != null && nuclearBombClip != null)
                explosionAudioSource.PlayOneShot(nuclearBombClip, 1f);

            if (bomb != null) Destroy(bomb);

            try { feedbackNuclearBlast?.PlayFeedbacks(); } catch (System.Exception e) { Debug.LogWarning($"[Ending] feedbackNuclearBlast failed: {e.Message}"); }
            StartCoroutine(AnimateNuclearVolume());
            StartCoroutine(SpawnNukeLight(impactPos));

            if (shockwavePrefab != null)
                StartCoroutine(SpawnShockwave(impactPos));

            // Óriási vakító villám a kamera irányába — azonnali fehér-out, ELŐSZÖR sül el
            StartCoroutine(SpawnBlindingFlash());
            yield return new WaitForSecondsRealtime(flashHoldDuration);

            // VFX a villám halvány mögül bontakozik ki — sokkal lassabb szimuláció
            if (vfxNuke != null)
            {
                vfxNuke.gameObject.SetActive(true);
                vfxNuke.transform.position = impactPos;
                vfxNuke.Stop(true);
                vfxNuke.Clear(true);
                vfxNuke.Play(true);
                var vfxMain = vfxNuke.main;
                vfxMain.simulationSpeed = nuclearVfxSimSpeed;
            }

            yield return new WaitForSecondsRealtime(postBlastHold);

            try { feedbackNuclearSettle?.PlayFeedbacks(); } catch (System.Exception e) { Debug.LogWarning($"[Ending] feedbackNuclearSettle failed: {e.Message}"); }
            SteamManager.Instance?.UnlockAchievement(SteamManager.Achievements.THE_HARD_WAY);

            yield return new WaitForSecondsRealtime(1.0f);

            // Restore normal time before fade-out
            Time.timeScale = 1f;

            // Stop all ending audio before scene reload to prevent stale playback
            if (airplaneAudioSource  != null) { airplaneAudioSource.Stop();  airplaneAudioSource.loop  = false; airplaneAudioSource.clip  = null; }
            if (explosionAudioSource != null) { explosionAudioSource.Stop(); explosionAudioSource.loop = false; }

            yield return StartCoroutine(FadeToBlack(fadeOutDuration));

            // Hide letterbox bars instantly (we're on black — no pop visible)
            if (letterboxTop    != null) letterboxTop.anchoredPosition    = new Vector2(0f,  letterboxHeight);
            if (letterboxBottom != null) letterboxBottom.anchoredPosition = new Vector2(0f, -letterboxHeight);

            // Wait for any in-flight background save before wiping
            if (SaveSystem.Instance != null)
                while (SaveSystem.Instance.IsSaveInFlight)
                    yield return null;

            // Wipe all save data so PlayAgain() reloads to a fresh game (no Continue button)
            SaveSystem.Instance?.DeleteSave();
            SaveSystem.Instance?.DeleteBackup();
            SteamManager.Instance?.CloudSave(string.Empty);

            // Show journal over the black screen, then fade in to reveal it
            EndScreen.PendingEndingType = EndScreen.EndingType.Nuclear;
            if (endScreenRoot != null) endScreenRoot.SetActive(true);
            yield return StartCoroutine(FadeFromBlack(0.5f));
        }

        // ======================================================================
        // Player camera tracking — body yaw + camera pitch toward moving target
        // ======================================================================

        /// <summary>Continuously rotates player body (yaw) and cameraRoot (pitch) toward target.</summary>
        IEnumerator TrackWithPlayerCamera(Transform target)
        {
            var pc = PlayerController.Instance;
            if (pc == null || target == null) yield break;

            while (target != null)
            {
                Vector3 toTarget = target.position - pc.transform.position;

                // Horizontal body rotation (yaw)
                Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
                if (flatDir.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredYaw = Quaternion.LookRotation(flatDir.normalized);
                    pc.transform.rotation = Quaternion.Slerp(
                        pc.transform.rotation, desiredYaw,
                        Time.unscaledDeltaTime * playerTrackSpeed);
                }

                // Vertical camera pitch
                if (pc.cameraRoot != null && toTarget.sqrMagnitude > 0.01f)
                {
                    float flatDist  = flatDir.magnitude;
                    float pitchDeg  = Mathf.Atan2(toTarget.y, flatDist) * Mathf.Rad2Deg;
                    pitchDeg = Mathf.Clamp(pitchDeg, -80f, 80f);
                    Quaternion desiredPitch = Quaternion.Euler(-pitchDeg, 0f, 0f);
                    pc.cameraRoot.localRotation = Quaternion.Slerp(
                        pc.cameraRoot.localRotation, desiredPitch,
                        Time.unscaledDeltaTime * playerTrackSpeed);
                }

                yield return null;
            }
        }

        // ======================================================================
        // FOV zoom
        // ======================================================================

        IEnumerator AnimateFOV(float targetFOV, float duration)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            float startFOV = cam.fieldOfView;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.fieldOfView = Mathf.Lerp(startFOV, targetFOV,
                    Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            cam.fieldOfView = targetFOV;
        }

        // ======================================================================
        // Airplane movement — rotation stays fixed (Inspector value)
        // ======================================================================

        IEnumerator MoveAirplane(Vector3 target, float duration)
        {
            if (airplaneTransform == null) yield break;

            Vector3 start       = airplaneTransform.position;
            Vector3 targetFlat  = new Vector3(target.x, start.y, target.z);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                airplaneTransform.position = Vector3.Lerp(start, targetFlat, t);
                airplaneTransform.rotation = AIRPLANE_FIXED_ROTATION; // hold exact Inspector rotation

                if (airplaneAudioSource != null)
                    airplaneAudioSource.volume = Mathf.Clamp01(t * 2f);

                yield return null;
            }

            airplaneTransform.position = targetFlat;
            airplaneTransform.rotation = AIRPLANE_FIXED_ROTATION;
        }

        /// <summary>Airplane continues flying past the drop point at the same speed.</summary>
        IEnumerator ContinueAirplane(float speed, float duration)
        {
            if (airplaneTransform == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                airplaneTransform.position += _airplaneTravelDir * speed * Time.unscaledDeltaTime;
                airplaneTransform.rotation  = AIRPLANE_FIXED_ROTATION;
                yield return null;
            }
        }

        // ======================================================================
        // Bomb drop — straight down (Y-axis), ease-in gravity feel
        // ======================================================================

        IEnumerator DropBombDown(GameObject bomb, Vector3 groundTarget)
        {
            if (bomb == null) yield break;

            var rb = bomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic    = true;
                rb.interpolation  = RigidbodyInterpolation.None; // prevent visual jitter when setting transform.position directly
            }

            Vector3 start   = bomb.transform.position;
            float   elapsed = 0f;
            while (elapsed < bombDropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / bombDropDuration;
                bomb.transform.position = Vector3.Lerp(start, groundTarget, t * t);
                yield return null;
            }
            bomb.transform.position = groundTarget;
        }

        /// <summary>Raycast down to find terrain/collider height at worldPos.</summary>
        Vector3 ComputeGroundPosition(Vector3 worldPos)
        {
            if (Physics.Raycast(worldPos + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 600f))
                return hit.point;
            if (Terrain.activeTerrain != null)
                return new Vector3(worldPos.x, Terrain.activeTerrain.SampleHeight(worldPos), worldPos.z);
            return new Vector3(worldPos.x, 0f, worldPos.z);
        }

        // ======================================================================
        // Player rotation (one-shot smooth turn)
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

            float fromTop = enter ?  letterboxHeight : 0f;
            float toTop   = enter ?  0f              : letterboxHeight;
            float fromBot = enter ? -letterboxHeight  : 0f;
            float toBot   = enter ?  0f              : -letterboxHeight;

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
        // Fade from black — reveals whatever is on screen below the overlay
        // ======================================================================

        IEnumerator FadeFromBlack(float duration)
        {
            if (_fadeImage == null) yield break;

            _fadeImage.gameObject.SetActive(true);
            _fadeImage.color = Color.black;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(elapsed / duration);
                _fadeImage.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }

            _fadeImage.color = Color.clear;
            _fadeImage.gameObject.SetActive(false);
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

            yield return new WaitForSecondsRealtime(0.15f);

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

        // ======================================================================
        // Nuke light — white flash → orange fire decay
        // ======================================================================

        IEnumerator SpawnNukeLight(Vector3 pos)
        {
            var lightGO     = new GameObject("NukeExplosionLight");
            lightGO.transform.position = pos + Vector3.up * 5f;
            var light       = lightGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.range     = nukeLightRange;
            light.color     = Color.white;
            light.intensity = nukeFlashIntensity;

            // White flash → orange over 0.3 s
            const float flashDuration = 0.3f;
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / flashDuration;
                light.color     = Color.Lerp(Color.white, nukeLightColor, t);
                light.intensity = Mathf.Lerp(nukeFlashIntensity, nukeLightIntensity, t);
                yield return null;
            }

            // Orange fire glow decays
            light.color     = nukeLightColor;
            light.intensity = nukeLightIntensity;
            elapsed = 0f;
            while (elapsed < nukePostDecayTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / nukePostDecayTime;
                light.intensity = Mathf.Lerp(nukeLightIntensity, 0f, t * t);
                yield return null;
            }

            Destroy(lightGO);
        }

        // ======================================================================
        // Shockwave expansion
        // ======================================================================

        IEnumerator SpawnShockwave(Vector3 pos)
        {
            if (shockwavePrefab == null) yield break;

            var sw      = Instantiate(shockwavePrefab, pos, Quaternion.identity);
            float elapsed = 0f;
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t     = elapsed / shockwaveDuration;
                float scale = Mathf.Lerp(0f, shockwaveMaxScale, 1f - Mathf.Pow(1f - t, 2f));
                sw.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            Destroy(sw);
        }

        // ======================================================================
        // Bomb-follow camera — cuts to a dedicated camera during the drop,
        // then restores the main camera after the explosion hold.
        // ======================================================================

        IEnumerator RunBombCamera(GameObject bomb, float dropDuration)
        {
            var mainCam = Camera.main;
            var pc      = PlayerController.Instance;

            // Save exact player camera state before switching to bomb cam
            Quaternion savedBodyRot = pc != null ? pc.transform.rotation : Quaternion.identity;
            Quaternion savedCamRot  = pc?.cameraRoot != null ? pc.cameraRoot.localRotation : Quaternion.identity;
            Debug.Log($"[BombCamera] START — mainCam={(mainCam != null ? mainCam.name : "NULL")} | " +
                      $"savedBodyRot={savedBodyRot.eulerAngles} | savedCamRot={savedCamRot.eulerAngles}");

            var go  = new GameObject("BombDropCamera");
            var cam = go.AddComponent<Camera>();

            if (mainCam != null)
            {
                cam.clearFlags      = mainCam.clearFlags;
                cam.backgroundColor = mainCam.backgroundColor;
                cam.cullingMask     = mainCam.cullingMask;
                cam.nearClipPlane   = mainCam.nearClipPlane;
                cam.farClipPlane    = mainCam.farClipPlane;
                cam.depth           = mainCam.depth + 2;
            }
            cam.fieldOfView = fovBomb;

            var urpData = cam.GetUniversalAdditionalCameraData();
            if (urpData != null) urpData.renderPostProcessing = true;

            if (mainCam != null) mainCam.enabled = false;

            // Side offset perpendicular to the airplane's travel direction (always a true side-view)
            Vector3 sideDir = _airplaneTravelDir.sqrMagnitude > 0.001f
                ? Vector3.Cross(_airplaneTravelDir, Vector3.up).normalized
                : Vector3.right;
            Vector3 relativeOffset = sideDir * bombCamOffset.magnitude;

            // Follow the bomb until preImpactCamRestoreTime seconds before it hits terrain
            float followDuration = Mathf.Max(0f, dropDuration - preImpactCamRestoreTime);
            float elapsed = 0f;
            while (bomb != null && elapsed < followDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                go.transform.position = bomb.transform.position + relativeOffset;
                Vector3 towardBomb = bomb.transform.position - go.transform.position;
                towardBomb.y = 0f;
                if (towardBomb.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.LookRotation(towardBomb.normalized);
                yield return null;
            }

            // Restore player camera to the exact state it was in before the bomb cam
            if (mainCam != null) mainCam.enabled = true;
            if (pc != null) pc.transform.rotation = savedBodyRot;
            if (pc?.cameraRoot != null) pc.cameraRoot.localRotation = savedCamRot;
            Debug.Log($"[BombCamera] mainCam restored — bodyRot={savedBodyRot.eulerAngles} camRot={savedCamRot.eulerAngles}");
            Destroy(go);
        }

        // ======================================================================
        // Blinding screen flash — instantaneous white-out that fades to clear.
        // ======================================================================

        IEnumerator SpawnBlindingFlash()
        {
            var go     = new GameObject("NukeBlindingFlash");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            go.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("FlashImage");
            imgGO.transform.SetParent(go.transform, false);
            var rt = imgGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = imgGO.AddComponent<Image>();
            img.color         = Color.white;
            img.raycastTarget = false;

            yield return new WaitForSecondsRealtime(flashHoldDuration);

            float elapsed = 0f;
            while (elapsed < flashFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = 1f - Mathf.SmoothStep(0f, 1f, elapsed / flashFadeDuration);
                img.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }

            Destroy(go);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        Vector3 GetFieldCenter()
        {
            if (grassFields == null || grassFields.Length == 0)
                return new Vector3(100f, 0f, 100f);

            Vector3 sum   = Vector3.zero;
            int     count = 0;
            foreach (var gf in grassFields)
            {
                if (gf == null) continue;
                // transform.position is the bottom-left origin — add half fieldSize to get true center
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

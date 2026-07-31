using UnityEngine;

namespace Fields.Audio
{
    /// <summary>
    /// Central audio manager for all tool and ambient sounds.
    /// Exposes per-clip dynamic controls (pitch offset, speed, volume).
    /// Swing sounds use random pitch in [basePitch, basePitch + swingPitchVariation].
    /// </summary>
    public class ToolAudioManager : MonoBehaviour
    {
        public static ToolAudioManager Instance { get; private set; }

        [Header("Melee Swing")]
        public AudioSource sickleScytheSwing;
        [Tooltip("Base pitch for swing sounds")]
        [Range(0.5f, 2f)]
        public float baseSwingPitch = 1.0f;
        [Tooltip("Max random pitch added per swing (0–0.7)")]
        [Range(0f, 0.7f)]
        public float swingPitchVariation = 0.7f;

        [Header("Powered Tool Loops")]
        public AudioSource stringTrimmerLoop;
        public AudioSource pushMowerLoop;
        public AudioSource tractorLoop;
        public AudioSource balerLoop;

        [Header("Player Movement")]
        public AudioSource footstepsWalk;
        public AudioSource footstepsRun;

        [Header("Economy")]
        public AudioSource moneyCollect;

        [Header("Ambient")]
        public AudioSource backgroundAmbient;

        [Header("Global Controls")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;
        [Tooltip("Pitch multiplier applied to all looping sources")]
        [Range(0.25f, 3f)]
        public float globalPitchMultiplier = 1f;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            StartBackground();
        }

        void Update()
        {
            SyncLoop(stringTrimmerLoop);
            SyncLoop(pushMowerLoop);
            SyncLoop(tractorLoop);
            SyncLoop(balerLoop);
            SyncLoop(backgroundAmbient);
            SyncLoop(footstepsWalk);
            SyncLoop(footstepsRun);
        }

        void SyncLoop(AudioSource src)
        {
            if (src == null) return;
            src.volume = masterVolume;
            src.pitch  = globalPitchMultiplier;
        }

        // ── Melee swing ───────────────────────────────────────────────────────

        public void PlaySwing()
        {
            if (sickleScytheSwing == null || sickleScytheSwing.clip == null) return;
            sickleScytheSwing.loop   = false;
            sickleScytheSwing.volume = masterVolume;
            sickleScytheSwing.pitch  = baseSwingPitch + Random.Range(0f, swingPitchVariation);
            sickleScytheSwing.Play();
        }

        public void StopSwing()
        {
            if (sickleScytheSwing == null || !sickleScytheSwing.isPlaying) return;
            sickleScytheSwing.Stop();
        }

        // ── Loops ─────────────────────────────────────────────────────────────

        public void StartStringTrimmer()  => StartLoop(stringTrimmerLoop);
        public void StopStringTrimmer()   => StopLoop(stringTrimmerLoop);

        public void StartPushMower()      => StartLoop(pushMowerLoop);
        public void StopPushMower()       => StopLoop(pushMowerLoop);

        public void StartTractor()        => StartLoop(tractorLoop);
        public void StopTractor()         => StopLoop(tractorLoop);

        public void StartBaler()          => StartLoop(balerLoop);
        public void StopBaler()           => StopLoop(balerLoop);

        public void StartFootstepsWalk()  => StartLoop(footstepsWalk);
        public void StopFootstepsWalk()   => StopLoop(footstepsWalk);

        public void StartFootstepsRun()   => StartLoop(footstepsRun);
        public void StopFootstepsRun()    => StopLoop(footstepsRun);

        public void PlayMoney()
        {
            if (moneyCollect == null || moneyCollect.clip == null) return;
            moneyCollect.PlayOneShot(moneyCollect.clip, masterVolume);
        }

        public void StartBackground()     => StartLoop(backgroundAmbient);
        public void StopBackground()      => StopLoop(backgroundAmbient);

        // ── Helpers ───────────────────────────────────────────────────────────

        void StartLoop(AudioSource src)
        {
            if (src == null || src.isPlaying) return;
            src.loop   = true;
            src.volume = masterVolume;
            src.pitch  = globalPitchMultiplier;
            src.Play();
        }

        void StopLoop(AudioSource src)
        {
            if (src == null || !src.isPlaying) return;
            src.Stop();
        }
    }
}

using UnityEngine;

namespace Fields.Audio
{
    /// <summary>
    /// Plays UI interaction sounds (click, etc.).
    /// Attach to any persistent GameObject in the scene; assign the AudioSource and clip in Inspector.
    /// </summary>
    public class UISoundManager : MonoBehaviour
    {
        public static UISoundManager Instance { get; private set; }

        [Header("UI Sounds")]
        public AudioSource audioSource;
        public AudioClip   clickClip;

        [Range(0f, 1f)]
        public float clickVolume = 0.7f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void PlayClick()
        {
            if (audioSource == null || clickClip == null) return;
            audioSource.PlayOneShot(clickClip, clickVolume);
        }

        /// <summary>Static shorthand — safe to call even before the scene is ready.</summary>
        public static void Click() => Instance?.PlayClick();
    }
}

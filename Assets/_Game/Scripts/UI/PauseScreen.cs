using Fields.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Pause menu screen. Pushed by PauseController on Esc / Start.
    ///
    /// Single-player : Time.timeScale = 0, audio ducked.
    /// Co-op         : timeScale untouched, translucent overlay, "game continues" notice.
    ///
    /// Reads SessionState.IsMultiplayer to decide behavior — one prefab for both modes.
    /// </summary>
    public class PauseScreen : UIScreen
    {
        [Header("Menu entries")]
        public Button resumeButton;
        public Button journalButton;
        public Button settingsButton;
        public Button quitButton;

        [Header("Linked screens")]
        public JournalScreen  journalScreen;
        public SettingsScreen settingsScreen;

        [Header("Co-op only (hidden in single-player)")]
        public Button inviteFriendButton;
        public Button playersButton;

        [Header("Co-op notice")]
        public GameObject coopNoticeRoot;
        public TextMeshProUGUI coopNoticeText;

        [Header("Background overlay")]
        [Tooltip("Semi-transparent dark panel behind the menu entries.")]
        public Image overlay;

        [Header("Audio")]
        [Tooltip("AudioMixer groups to duck while paused (World, Tools).")]
        public UnityEngine.Audio.AudioMixer masterMixer;
        [Tooltip("dB value for ducked groups (e.g. -14)")]
        public float duckdB = -14f;

        const string WORLD_VOLUME_PARAM   = "WorldVolume";
        const string TOOLS_VOLUME_PARAM   = "ToolsVolume";
        const string AMBIENT_VOLUME_PARAM = "AmbienceVolume";

        // Original mixer values restored on resume
        float _origWorld, _origTools;

        // ------------------------------------------------------------------ //
        // UIScreen lifecycle
        // ------------------------------------------------------------------ //

        protected override void Awake()
        {
            base.Awake();

            if (resumeButton)      resumeButton.onClick.AddListener(ClickResume);
            if (journalButton)     journalButton.onClick.AddListener(OnJournal);
            if (settingsButton)    settingsButton.onClick.AddListener(OnSettings);
            if (quitButton)        quitButton.onClick.AddListener(OnQuit);
            if (inviteFriendButton) inviteFriendButton.onClick.AddListener(OnInviteFriend);
        }

        protected override void OnScreenPushed()
        {
            bool mp = SessionState.Instance != null && SessionState.Instance.IsMultiplayer;

            // Show/hide co-op-only buttons
            if (inviteFriendButton) inviteFriendButton.gameObject.SetActive(mp);
            if (playersButton)      playersButton.gameObject.SetActive(mp);
            if (coopNoticeRoot)     coopNoticeRoot.SetActive(mp);

            if (mp)
            {
                // Co-op: game continues — only show translucent overlay
                if (overlay) overlay.color = new Color(0f, 0f, 0f, 0.55f);
                if (coopNoticeText)
                    coopNoticeText.text = Fields.Core.LocalizationManager.Instance != null
                        ? Fields.Core.LocalizationManager.Instance.Get("pause.coop_notice")
                        : "The game continues while this menu is open.";
            }
            else
            {
                // Single-player: pause time and duck audio
                Time.timeScale = 0f;
                DuckAudio(true);
                if (overlay) overlay.color = new Color(0f, 0f, 0f, 0.75f);
            }
        }

        protected override void OnScreenClosed()
        {
            bool mp = SessionState.Instance != null && SessionState.Instance.IsMultiplayer;
            if (!mp)
            {
                Time.timeScale = 1f;
                DuckAudio(false);
            }
        }

        // Resuming from a sub-screen (e.g. Settings → back) doesn't un-pause —
        // we're still in the pause stack. OnScreenResumed is intentionally empty.

        // ------------------------------------------------------------------ //
        // Button handlers
        // ------------------------------------------------------------------ //

        void ClickResume() => UIManager.Instance?.PopAll();

        void OnJournal()
        {
            if (journalScreen != null)
                UIManager.Instance?.Push(journalScreen);
        }

        void OnSettings()
        {
            if (settingsScreen != null)
                UIManager.Instance?.Push(settingsScreen);
        }

        void OnQuit()
        {
            bool mp   = SessionState.Instance != null && SessionState.Instance.IsMultiplayer;
            bool host = !mp; // single-player is always "host"

            if (mp && !host)
            {
                // Guest: just leave
                Fields.Network.CoopSessionManager.Instance?.LeaveGame();
                UIManager.Instance?.PopAll();
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                return;
            }

            // Host or single-player: autosave then return to main menu.
            // Co-op host needs confirmation modal (future task).
            Fields.Save.SaveSystem.Instance?.SaveGame();
            UIManager.Instance?.PopAll();

            // For now: reload scene (main menu scene doesn't exist yet).
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void OnInviteFriend()
        {
#if !DISABLESTEAMWORKS
            Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(default);
#endif
            Debug.Log("[PauseScreen] Steam invite overlay — wired in Stage 2.");
        }

        // ------------------------------------------------------------------ //
        // Gamepad default focus
        // ------------------------------------------------------------------ //

        protected override GameObject GetDefaultFocus() =>
            resumeButton != null ? resumeButton.gameObject : null;

        // ------------------------------------------------------------------ //
        // Audio ducking
        // ------------------------------------------------------------------ //

        void DuckAudio(bool duck)
        {
            if (masterMixer == null) return;
            if (duck)
            {
                masterMixer.GetFloat(WORLD_VOLUME_PARAM,   out _origWorld);
                masterMixer.GetFloat(TOOLS_VOLUME_PARAM,   out _origTools);
                masterMixer.SetFloat(WORLD_VOLUME_PARAM,   duckdB);
                masterMixer.SetFloat(TOOLS_VOLUME_PARAM,   duckdB);
                // Ambience stays at ~60 % — silence is jarring in this genre
            }
            else
            {
                masterMixer.SetFloat(WORLD_VOLUME_PARAM,   _origWorld);
                masterMixer.SetFloat(TOOLS_VOLUME_PARAM,   _origTools);
            }
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Co-op submenu pushed from the main menu.
    /// UI is complete; netcode is stubbed until Stage 2.
    ///
    /// Two tabs: Host (lobby setup) and Join (friends' lobby list).
    /// Both panels show persistent notices explaining guest/host save semantics.
    /// </summary>
    public class CoopScreen : UIScreen
    {
        [Header("Tabs")]
        public Button      hostTabButton;
        public Button      joinTabButton;
        public GameObject  hostPanel;
        public GameObject  joinPanel;

        [Header("Host panel")]
        public Button   startButton;
        public Button   inviteButton;
        public Button[] visibilityButtons;   // 0 = Friends Only, 1 = Invite Only

        [Header("Join panel")]
        public TextMeshProUGUI emptyStateText;

        [Header("Notice (shown on both panels)")]
        public TextMeshProUGUI noticeText;

        [Header("Navigation")]
        public Button backButton;

        // Persistent notice text — shown on both panels (spec §3).
        const string NOTICE =
            "Guests start with base tools and their progress is not saved.\n" +
            "The host owns the save file.";

        protected override void Awake()
        {
            base.Awake();
            if (hostTabButton) hostTabButton.onClick.AddListener(() => SelectTab(0));
            if (joinTabButton) joinTabButton.onClick.AddListener(() => SelectTab(1));
            if (backButton)    backButton.onClick.AddListener(() => UIManager.Instance?.Pop());
            if (inviteButton)  inviteButton.onClick.AddListener(OnInvite);
            if (startButton)   startButton.onClick.AddListener(OnStart);
        }

        protected override void OnScreenPushed()
        {
            if (noticeText) noticeText.text = NOTICE;
            SelectTab(0);
        }

        // ------------------------------------------------------------------ //

        void SelectTab(int index)
        {
            if (hostPanel) hostPanel.SetActive(index == 0);
            if (joinPanel) joinPanel.SetActive(index == 1);

            if (emptyStateText && index == 1)
                emptyStateText.text =
                    "No joinable lobbies found.\nAsk a friend to host and invite you.";
        }

        void OnInvite()
        {
#if !DISABLESTEAMWORKS
            Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(default);
#endif
            Debug.Log("[CoopScreen] Steam invite overlay — wired in Stage 2.");
        }

        void OnStart()
        {
            Debug.Log("[CoopScreen] Start lobby — wired in Stage 2.");
        }

        protected override GameObject GetDefaultFocus() =>
            hostTabButton != null ? hostTabButton.gameObject : null;
    }
}
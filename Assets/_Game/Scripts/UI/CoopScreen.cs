using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fields.Network;

namespace Fields.UI
{
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

        [Header("Status")]
        [Tooltip("Optional — shows lobby creation status below the Start button")]
        public TextMeshProUGUI statusText;

        [Header("Navigation")]
        public Button backButton;


        protected override void Awake()
        {
            base.Awake();
            if (hostTabButton) hostTabButton.onClick.AddListener(() => SelectTab(0));
            if (joinTabButton) joinTabButton.onClick.AddListener(() => SelectTab(1));
            if (backButton)    backButton.onClick.AddListener(OnBack);
            if (inviteButton)  inviteButton.onClick.AddListener(OnInvite);
            if (startButton)   startButton.onClick.AddListener(OnStart);
        }

        protected override void OnScreenPushed()
        {
            RefreshNotice();
            SelectTab(0);

            // Invite only becomes active once a lobby exists
            if (inviteButton) inviteButton.interactable = false;
            if (startButton)  startButton.interactable  = true;
            SetStatus("");

            var csm = CoopSessionManager.Instance;
            if (csm != null) csm.OnLobbyReady += HandleLobbyReady;
        }

        protected override void OnScreenClosed()
        {
            var csm = CoopSessionManager.Instance;
            if (csm != null) csm.OnLobbyReady -= HandleLobbyReady;
        }

        void HandleLobbyReady()
        {
            if (inviteButton) inviteButton.interactable = true;
            SetStatus("Lobby ready — invite friends!");
        }

        void RefreshNotice()
        {
            if (noticeText == null) return;
            var loc = Fields.Core.LocalizationManager.Instance;
            noticeText.text = loc != null ? loc.Get("coop.notice")
                : "Guests start with base tools and their progress is not saved.\nThe host owns the save file.";
        }

        // ------------------------------------------------------------------ //

        void SelectTab(int index)
        {
            if (hostPanel) hostPanel.SetActive(index == 0);
            if (joinPanel) joinPanel.SetActive(index == 1);

            if (emptyStateText && index == 1)
            {
                var loc = Fields.Core.LocalizationManager.Instance;
                emptyStateText.text = loc != null ? loc.Get("coop.join.empty")
                    : "No joinable lobbies found.\nAsk a friend to host and invite you.";
            }
        }

        void OnBack()
        {
            var csm = CoopSessionManager.Instance;
            if (csm != null && csm.IsLobbyActive)
                csm.LeaveGame();

            UIManager.Instance?.Pop();
        }

        void OnStart()
        {
            if (startButton) startButton.interactable = false;
            SetStatus("Creating lobby...");
            CoopSessionManager.Instance?.HostGame();
        }

        void OnInvite()
        {
#if !DISABLESTEAMWORKS
            var csm = CoopSessionManager.Instance;
            if (csm != null && csm.IsLobbyActive)
                Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(csm.CurrentLobby);
            else
                Debug.LogWarning("[CoopScreen] Invite pressed but no active lobby.");
#endif
        }

        void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        protected override GameObject GetDefaultFocus() =>
            hostTabButton != null ? hostTabButton.gameObject : null;
    }
}

using Fields.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Main menu screen. Pushed by WorldBootstrap on scene load before the player can move.
    ///
    /// Entries (spec §3):
    ///   Continue       — hidden entirely when no save exists; default focus when save found.
    ///   New Game       — default focus when no save; shows overwrite confirm if save exists.
    ///   Play Friends   — always visible; opens co-op submenu (stubbed until Stage 2).
    ///   Settings       — reuses the same SettingsScreen instance as the pause menu.
    ///   Credits        — scrollable, skippable.
    ///   Quit           — requires confirm.
    ///
    /// Camera: a separate menu camera (IdleCameraDrift) is active while this screen is open.
    /// On game start it is disabled and the player root (containing the player camera) is enabled.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        [Header("Buttons")]
        public Button continueButton;
        public Button newGameButton;
        public Button playWithFriendsButton;
        public Button settingsButton;
        public Button creditsButton;
        public Button quitButton;

        [Header("Linked screens")]
        public SettingsScreen settingsScreen;
        public CoopScreen     coopScreen;
        public CreditsScreen  creditsScreen;
        public ConfirmModal   confirmModal;

        [Header("Scene objects")]
        [Tooltip("Menu camera GO with IdleCameraDrift — disabled when game starts.")]
        public GameObject menuCameraRoot;
        [Tooltip("Player GO — hidden while the menu is open, enabled on game start.")]
        public GameObject playerRoot;

        protected override void Awake()
        {
            base.Awake();

            if (continueButton)        continueButton.onClick.AddListener(ClickContinue);
            if (newGameButton)         newGameButton.onClick.AddListener(ClickNewGame);
            if (playWithFriendsButton) playWithFriendsButton.onClick.AddListener(ClickCoop);
            if (settingsButton)        settingsButton.onClick.AddListener(ClickSettings);
            if (creditsButton)         creditsButton.onClick.AddListener(ClickCredits);
            if (quitButton)            quitButton.onClick.AddListener(ClickQuit);
        }

        protected override void OnScreenPushed()
        {
            // Continue only makes sense when a save exists.
            bool hasSave = SaveSystem.HasSave;
            if (continueButton) continueButton.gameObject.SetActive(hasSave);

            if (menuCameraRoot) menuCameraRoot.SetActive(true);
        }

        // ------------------------------------------------------------------ //
        // Button handlers
        // ------------------------------------------------------------------ //

        void ClickContinue()
        {
            // WorldBootstrap already called LoadGame() — the world state is restored.
            // Dismissing the menu is all that is needed.
            StartGame();
        }

        void ClickNewGame()
        {
            if (SaveSystem.HasSave)
            {
                var loc = Fields.Core.LocalizationManager.Instance;
                confirmModal?.Show(
                    header:    loc != null ? loc.Get("menu.overwrite.header") : "New Game",
                    body:      loc != null ? loc.Get("menu.overwrite.body")   : "Starting a new game will overwrite your existing save. This cannot be undone.",
                    onConfirm: () =>
                    {
                        SaveSystem.Instance?.DeleteSave();
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    });
            }
            else
            {
                StartGame();
            }
        }

        void ClickCoop()
        {
            if (coopScreen != null)
                UIManager.Instance?.Push(coopScreen);
        }

        void ClickSettings()
        {
            if (settingsScreen != null)
                UIManager.Instance?.Push(settingsScreen);
        }

        void ClickCredits()
        {
            if (creditsScreen != null)
                UIManager.Instance?.Push(creditsScreen);
        }

        void ClickQuit()
        {
            var loc = Fields.Core.LocalizationManager.Instance;
            confirmModal?.Show(
                header:    loc != null ? loc.Get("menu.quit.header") : "Quit",
                body:      loc != null ? loc.Get("menu.quit.body")   : "Are you sure you want to quit?",
                onConfirm: () =>
                {
                    SaveSystem.Instance?.SaveGame();
                    Application.Quit();
                });
        }

        // ------------------------------------------------------------------ //
        // Game start
        // ------------------------------------------------------------------ //

        void StartGame()
        {
            Fields.Core.SessionState.Instance?.StartSinglePlayer();
            Fields.Core.RecordsManager.Instance?.ResetSessionState();
            if (menuCameraRoot) menuCameraRoot.SetActive(false);
            if (playerRoot)     playerRoot.SetActive(true);
            UIManager.Instance?.PopAll();
        }

        // ------------------------------------------------------------------ //

        protected override GameObject GetDefaultFocus()
        {
            // Continue is the natural first action when a save exists.
            if (SaveSystem.HasSave && continueButton != null && continueButton.gameObject.activeSelf)
                return continueButton.gameObject;
            return newGameButton != null ? newGameButton.gameObject : null;
        }
    }
}
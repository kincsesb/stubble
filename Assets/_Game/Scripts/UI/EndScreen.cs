using Fields.Core;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Activated by WorldBootstrap when all 4 parcels are complete.
    /// Shows final stats, offers Play Again / Quit.
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        [Header("Text fields")]
        public TextMeshProUGUI totalEarningsText;
        public TextMeshProUGUI timePlayedText;
        public TextMeshProUGUI titleText;

        [Header("Buttons")]
        public Button playAgainButton;
        public Button quitButton;

        static float _gameStartTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RecordStartTime() => _gameStartTime = Time.realtimeSinceStartup;

        void OnEnable()
        {
            Time.timeScale = 0f;

            // Stats
            int money = CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : 0;
            float elapsed = Time.realtimeSinceStartup - _gameStartTime;
            int mins = Mathf.FloorToInt(elapsed / 60f);
            int secs = Mathf.FloorToInt(elapsed % 60f);

            if (totalEarningsText != null)
                totalEarningsText.text = Fields.Core.LocalizationManager.Instance != null
                    ? Fields.Core.LocalizationManager.Instance.Get("end.earnings", money)
                    : $"Total earnings: $ {money}";

            if (timePlayedText != null)
                timePlayedText.text = Fields.Core.LocalizationManager.Instance != null
                    ? Fields.Core.LocalizationManager.Instance.Get("end.time", mins, secs)
                    : $"Time: {mins:00}:{secs:00}";

            if (titleText != null)
                titleText.text = Fields.Core.LocalizationManager.Instance != null
                    ? Fields.Core.LocalizationManager.Instance.Get("end.title")
                    : "All fields cleared!";

            // Speed-run achievement (sub-30 minutes)
            if (elapsed < 1800f)
                Fields.Core.SteamManager.Instance?.UnlockAchievement(
                    Fields.Core.SteamManager.Achievements.ACH_SPEED_RUN);

            // Button labels
            SetButtonLabel(playAgainButton, "end.playagain", "Play Again");
            SetButtonLabel(quitButton,      "end.quit",      "Quit");

            // Wire buttons
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
        }

        static void SetButtonLabel(Button btn, string key, string fallback)
        {
            if (btn == null) return;
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp == null) return;
            tmp.text = Fields.Core.LocalizationManager.Instance != null
                ? Fields.Core.LocalizationManager.Instance.Get(key)
                : fallback;
        }

        void PlayAgain()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

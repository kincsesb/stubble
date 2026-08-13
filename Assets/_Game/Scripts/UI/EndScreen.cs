using Fields.Core;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Shown when the whole field is cleared.
    /// Displays final stats + a snarky localized comment.
    /// Set PendingEndingType before activating so BuildComment picks the right branch.
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        public enum EndingType { Peaceful, Loop, Nuclear }
        public static EndingType PendingEndingType = EndingType.Peaceful;
        [Header("Text fields")]
        public TextMeshProUGUI totalEarningsText;
        public TextMeshProUGUI timePlayedText;
        public TextMeshProUGUI titleText;
        [Tooltip("Optional: assign a TMP text field to show the end-of-game comment.")]
        public TextMeshProUGUI commentText;

        [Header("Buttons")]
        public Button playAgainButton;
        public Button quitButton;

        static float _gameStartTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RecordStartTime() => _gameStartTime = Time.realtimeSinceStartup;

        void OnEnable()
        {
            Time.timeScale = 0f;

            var loc   = LocalizationManager.Instance;
            var cur   = CurrencyManager.Instance;
            var ss    = SessionState.Instance;

            int   money   = cur  != null ? cur.Money : 0;
            float elapsed = Time.realtimeSinceStartup - _gameStartTime;
            int   mins    = Mathf.FloorToInt(elapsed / 60f);
            int   secs    = Mathf.FloorToInt(elapsed % 60f);

            // Collect stats for comment selection
            int totalBales = 0;
            int totalSwings = 0;
            if (ss != null)
            {
                var player = ss.GetPlayer(ss.LocalPlayerId);
                if (player != null)
                {
                    totalBales  = (int)(player.SquareBalesMade + player.RoundBalesMade);
                    totalSwings = (int)player.TotalSwings;
                }
            }

            if (totalEarningsText != null)
                totalEarningsText.text = loc != null
                    ? loc.Get("end.earnings", money)
                    : $"Total earnings: ${money}";

            if (timePlayedText != null)
                timePlayedText.text = loc != null
                    ? loc.Get("end.time", mins, secs)
                    : $"Time: {mins:00}:{secs:00}";

            if (titleText != null)
                titleText.text = loc != null
                    ? loc.Get("end.title")
                    : "All Fields Cleared!";

            if (commentText != null)
                commentText.text = BuildComment(loc, elapsed, money, totalBales, totalSwings, mins, secs);

            // Speed-run achievement
            if (elapsed < SteamManager.Thresholds.SPEEDRUN_SECONDS)
                SteamManager.Instance?.UnlockAchievement(SteamManager.Achievements.SPEED_RUN);

            SetButtonLabel(playAgainButton, "end.playagain", "Play Again");
            SetButtonLabel(quitButton,      "end.quit",      "Quit");

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
        }

        static string BuildComment(
            LocalizationManager loc, float elapsed, int money,
            int totalBales, int totalSwings, int mins, int secs)
        {
            if (loc == null) return string.Empty;

            // Theatrical ending overrides — checked first
            if (PendingEndingType == EndingType.Nuclear)
            {
                int hours = Mathf.FloorToInt(elapsed / 3600f);
                return loc.Get("end.comment.nuclear", hours);
            }

            // Priority-ordered comment selection
            if (elapsed < 20f * 60f)
                return loc.Get("end.comment.speedrun", mins, secs);

            if (elapsed > 65f * 60f)
                return loc.Get("end.comment.veteran", mins);

            if (totalBales >= 100)
                return loc.Get("end.comment.hundredbales", totalBales);

            if (money >= 10000)
                return loc.Get("end.comment.rich", money);

            // Lazy farmer: high money relative to swings means they used the ride-on
            if (totalSwings > 0 && money > 2000 && (float)money / totalSwings > 20f)
                return loc.Get("end.comment.efficient");

            // General pool — deterministic pick based on session data
            int pool    = 6;
            int pick    = Mathf.Abs((totalBales * 7 + mins * 13 + money) % pool);
            return loc.Get($"end.comment.{pick}");
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

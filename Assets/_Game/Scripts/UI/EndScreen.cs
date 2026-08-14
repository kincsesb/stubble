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
    ///
    /// After a Nuclear ending the save is already deleted before this screen opens,
    /// so the main menu will show only New Game (no Continue).
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        public enum EndingType { Peaceful, Loop, Nuclear }
        public static EndingType PendingEndingType = EndingType.Peaceful;

        [Header("Required text fields")]
        public TextMeshProUGUI totalEarningsText;
        public TextMeshProUGUI timePlayedText;
        public TextMeshProUGUI titleText;

        [Header("Stats content (assign ScrollView Content RectTransform)")]
        [Tooltip("StatsRenderer fills this with per-player sections. Works in singleplayer and co-op.")]
        public RectTransform statsContent;

        [Header("Optional legacy stat fields")]
        [Tooltip("e.g. 'Grass cut: 4 200 m2'")]
        public TextMeshProUGUI grassCutText;

        [Tooltip("e.g. 'Bales: 38'")]
        public TextMeshProUGUI balesMadeText;

        [Tooltip("e.g. 'Swings: 1 450'")]
        public TextMeshProUGUI totalSwingsText;

        [Tooltip("e.g. 'Distance: 12.4 km'")]
        public TextMeshProUGUI distanceTravelledText;

        [Tooltip("Optional: assign a TMP text field to show the end-of-game comment.")]
        public TextMeshProUGUI commentText;

        [Header("Buttons")]
        public Button playAgainButton;
        public Button quitButton;

        static float _gameStartTime;

        // gridCellSize default from GameConfig — used to convert cut cells to m².
        const float CELL_SIZE_M = 0.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RecordStartTime() => _gameStartTime = Time.realtimeSinceStartup;

        void OnEnable()
        {
            Time.timeScale = 0f;

            var loc = LocalizationManager.Instance;
            var cur = CurrencyManager.Instance;
            var ss  = SessionState.Instance;

            int   money   = cur  != null ? cur.Money : 0;
            float elapsed = Time.realtimeSinceStartup - _gameStartTime;
            int   mins    = Mathf.FloorToInt(elapsed / 60f);
            int   secs    = Mathf.FloorToInt(elapsed % 60f);

            // ── Collect per-player stats ──────────────────────────────────
            long  totalBales    = 0;
            long  totalSwings   = 0;
            long  totalCutCells = 0;
            double totalDistM   = 0;

            if (ss != null)
            {
                foreach (var p in ss.Players)
                {
                    if (p == null) continue;
                    totalBales    += p.SquareBalesMade + p.RoundBalesMade;
                    totalSwings   += p.TotalSwings;
                    totalCutCells += p.AreaCutCells;
                    totalDistM    += p.DistanceTravelledM;
                }
            }

            float cutAreaM2   = totalCutCells * CELL_SIZE_M * CELL_SIZE_M;
            float distKm      = (float)(totalDistM / 1000.0);

            // ── Per-player stats (StatsRenderer) ─────────────────────────
            StatsRenderer.Build(statsContent);

            // ── Required fields ───────────────────────────────────────────
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

            // ── Optional stat fields ──────────────────────────────────────
            if (grassCutText != null)
                grassCutText.text = loc != null
                    ? loc.Get("end.grass_cut", cutAreaM2)
                    : $"Grass cut: {cutAreaM2:N0} m²";

            if (balesMadeText != null)
                balesMadeText.text = loc != null
                    ? loc.Get("end.bales", totalBales)
                    : $"Bales: {totalBales}";

            if (totalSwingsText != null)
                totalSwingsText.text = loc != null
                    ? loc.Get("end.swings", totalSwings)
                    : $"Swings: {totalSwings}";

            if (distanceTravelledText != null)
                distanceTravelledText.text = loc != null
                    ? loc.Get("end.distance", distKm)
                    : $"Distance: {distKm:F1} km";

            if (commentText != null)
                commentText.text = BuildComment(loc, elapsed, money,
                    (int)totalBales, (int)totalSwings, mins, secs);

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

            if (PendingEndingType == EndingType.Nuclear)
            {
                int hours = Mathf.FloorToInt(elapsed / 3600f);
                return loc.Get("end.comment.nuclear", hours);
            }

            if (elapsed < 20f * 60f)
                return loc.Get("end.comment.speedrun", mins, secs);

            if (elapsed > 65f * 60f)
                return loc.Get("end.comment.veteran", mins);

            if (totalBales >= 100)
                return loc.Get("end.comment.hundredbales", totalBales);

            if (money >= 10000)
                return loc.Get("end.comment.rich", money);

            if (totalSwings > 0 && money > 2000 && (float)money / totalSwings > 20f)
                return loc.Get("end.comment.efficient");

            int pool = 6;
            int pick = Mathf.Abs((totalBales * 7 + mins * 13 + money) % pool);
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
            tmp.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(key)
                : fallback;
        }

        void PlayAgain()
        {
            Time.timeScale = 1f;
            // Reload scene → WorldBootstrap shows the main menu.
            // For nuclear ending the save was already deleted, so Continue won't appear.
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

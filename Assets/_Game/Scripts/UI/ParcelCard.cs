using Fields.Core;
using Fields.Economy;
using Fields.Grass;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// One card in the Journal's Parcels tab.
    /// Refresh(parcelIndex) is called by JournalScreen — never polls.
    ///
    /// Completion % is read from the CPU GrassField (authoritative per spec §6.1).
    /// All other stats come from SessionState.
    /// </summary>
    public class ParcelCard : MonoBehaviour
    {
        [Header("Grass field for completion % (CPU grid — assign in Inspector)")]
        public GrassField grassField;

        [Header("Locked state")]
        public GameObject lockedOverlay;
        public TextMeshProUGUI unlockPriceText;

        [Header("Unlocked — identity")]
        public TextMeshProUGUI parcelNameText;
        public TextMeshProUGUI statusText;

        [Header("Unlocked — grass")]
        public TextMeshProUGUI completionPctText;
        public TextMeshProUGUI areaCutText;
        public TextMeshProUGUI areaRemainingText;

        [Header("Unlocked — hay")]
        public TextMeshProUGUI haySpawnedText;
        public TextMeshProUGUI hayCollectedText;
        public TextMeshProUGUI hayInFieldText;    // highlighted: zero = completion condition met

        [Header("Unlocked — bales & economy")]
        public TextMeshProUGUI squareBalesText;
        public TextMeshProUGUI roundBalesText;
        public TextMeshProUGUI moneyEarnedText;
        public TextMeshProUGUI timeSpentText;

        [Header("Completion condition ticks")]
        public GameObject grassCutTick;
        public GameObject hayClearTick;

        [Header("Hay-in-field highlight color")]
        public Color hayRemainingColor = new Color(1f, 0.65f, 0.1f);
        public Color hayClearColor     = new Color(0.3f, 1f, 0.4f);

        // 0.4 m × 0.4 m cells → 0.16 m² (GrassField spec §8.4)
        const float CELL_AREA_M2 = 0.16f;

        static readonly string[] ParcelNames =
        {
            "Home Paddock", "Middle Meadow", "Far Meadow", "Top Meadow"
        };

        public void Refresh(int parcelIndex)
        {
            bool unlocked = ParcelManager.Instance == null || ParcelManager.Instance.IsUnlocked(parcelIndex);

            if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);

            if (!unlocked)
            {
                ShowUnlockedContent(false);
                ShowLockPrice(parcelIndex);
                return;
            }

            ShowUnlockedContent(true);

            var parcel = SessionState.Instance?.GetParcel(parcelIndex);
            if (parcel == null) return;

            // Name
            string name = parcelIndex < ParcelNames.Length ? ParcelNames[parcelIndex] : $"Parcel {parcelIndex}";
            SetText(parcelNameText, name);

            // Completion % — authoritative CPU grid; fallback to SessionState counters
            float pct;
            long totalCells;
            if (grassField != null)
            {
                pct        = grassField.GetCompletionPercent();
                totalCells = (long)grassField.GridCols * grassField.GridRows;
            }
            else
            {
                totalCells = parcel.AreaTotalCells;
                pct = totalCells > 0 ? (float)parcel.AreaCutCells / totalCells * 100f : 0f;
            }

            SetText(completionPctText, $"{pct:F1}%");

            long cut       = parcel.AreaCutCells;
            long remaining = totalCells - cut;
            SetText(areaCutText,       $"{cut * CELL_AREA_M2:F0} m²");
            SetText(areaRemainingText, $"{remaining * CELL_AREA_M2:F0} m²");

            // Hay
            SetText(haySpawnedText,   $"{parcel.HayPilesSpawned}");
            SetText(hayCollectedText, $"{parcel.HayPilesCollected}");

            long inField = parcel.HayPilesInField;
            if (hayInFieldText != null)
            {
                hayInFieldText.text  = inField > 0 ? $"{inField}" : "Clear";
                hayInFieldText.color = inField > 0 ? hayRemainingColor : hayClearColor;
            }

            // Bales & economy
            SetText(squareBalesText, $"{parcel.SquareBalesMade}");
            SetText(roundBalesText,  $"{parcel.RoundBalesMade}");
            SetText(moneyEarnedText, $"${parcel.MoneyEarned}");
            SetText(timeSpentText,   FormatTime(parcel.TimeSpentSeconds));

            // Status
            bool complete = parcel.IsCompleted;
            SetText(statusText, complete ? "Complete" : "In Progress");

            // Completion condition ticks
            if (grassCutTick != null) grassCutTick.SetActive(parcel.GrassCutComplete);
            if (hayClearTick != null) hayClearTick.SetActive(parcel.HayClearComplete);
        }

        // ------------------------------------------------------------------ //

        void ShowUnlockedContent(bool show)
        {
            // Toggle all named text fields; children with lockedOverlay handle locked state
            if (parcelNameText    != null) parcelNameText.gameObject.SetActive(show);
            if (statusText        != null) statusText.gameObject.SetActive(show);
            if (completionPctText != null) completionPctText.gameObject.SetActive(show);
            if (areaCutText       != null) areaCutText.gameObject.SetActive(show);
            if (areaRemainingText != null) areaRemainingText.gameObject.SetActive(show);
            if (haySpawnedText    != null) haySpawnedText.gameObject.SetActive(show);
            if (hayCollectedText  != null) hayCollectedText.gameObject.SetActive(show);
            if (hayInFieldText    != null) hayInFieldText.gameObject.SetActive(show);
            if (squareBalesText   != null) squareBalesText.gameObject.SetActive(show);
            if (roundBalesText    != null) roundBalesText.gameObject.SetActive(show);
            if (moneyEarnedText   != null) moneyEarnedText.gameObject.SetActive(show);
            if (timeSpentText     != null) timeSpentText.gameObject.SetActive(show);
            if (grassCutTick      != null) grassCutTick.SetActive(show);
            if (hayClearTick      != null) hayClearTick.SetActive(show);
        }

        void ShowLockPrice(int parcelIndex)
        {
            if (unlockPriceText == null || ParcelManager.Instance == null) return;
            var data = ParcelManager.Instance.parcels.Length > parcelIndex
                ? ParcelManager.Instance.parcels[parcelIndex]
                : null;
            unlockPriceText.text = data != null ? $"Unlock: ${data.unlockCost}" : "";
        }

        static void SetText(TextMeshProUGUI t, string v) { if (t != null) t.text = v; }

        static string FormatTime(float seconds)
        {
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            return h > 0 ? $"{h}h {m:D2}m" : $"{m}m {s:D2}s";
        }
    }
}

using Fields.Core;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Journal screen — J key / View-tap toggle.
    /// Three tabs: Parcels, Statistics, Records.
    /// All data read from SessionState; never polls — refreshes on open/tab switch only.
    /// </summary>
    public class JournalScreen : UIScreen
    {
        [Header("Tab navigation")]
        public Button[] tabButtons;       // [0]=Parcels [1]=Statistics [2]=Records
        public GameObject[] tabPanels;    // parallel to tabButtons

        [Header("Parcel tab")]
        public ParcelCard[] parcelCards;  // 4 cards, index matches parcel (0-3)

        [Header("Statistics tab — per player")]
        public TextMeshProUGUI statAreaCut;
        public TextMeshProUGUI statHayCollected;
        public TextMeshProUGUI statSquareBales;
        public TextMeshProUGUI statRoundBales;
        public TextMeshProUGUI statMoneyEarned;
        public TextMeshProUGUI statMoneySpent;
        public TextMeshProUGUI statDistance;
        public TextMeshProUGUI statSwings;
        public TextMeshProUGUI statPlaytime;

        [Header("Statistics tab — session-wide")]
        public TextMeshProUGUI statWallet;
        public TextMeshProUGUI statParcelsCompleted;
        public TextMeshProUGUI statOverallCompletion;
        public TextMeshProUGUI statTotalPlaytime;

        [Header("Records tab")]
        public TextMeshProUGUI recFastestParcel0;
        public TextMeshProUGUI recFastestParcel1;
        public TextMeshProUGUI recFastestParcel2;
        public TextMeshProUGUI recFastestParcel3;
        public TextMeshProUGUI recLargestArea;
        public TextMeshProUGUI recMostBales;
        public TextMeshProUGUI recLongestStreak;
        public TextMeshProUGUI recFullGame;

        // 0.4 m × 0.4 m cells → 0.16 m² per cell (matches GrassField spec §8.4)
        const float CELL_AREA_M2 = 0.16f;

        int _activeTab;
        bool _didPause;

        // ------------------------------------------------------------------ //
        // UIScreen lifecycle
        // ------------------------------------------------------------------ //

        protected override void OnScreenPushed()
        {
            // Pause gameplay if not already paused (e.g. opened directly via J key, not from Pause menu).
            if (Time.timeScale > 0f)
            {
                Time.timeScale = 0f;
                _didPause = true;
            }
            SelectTab(0);
            RefreshAll();
        }

        protected override void OnScreenClosed()
        {
            if (_didPause)
            {
                Time.timeScale = 1f;
                _didPause = false;
            }
        }

        protected override void OnScreenResumed()
        {
            RefreshAll();
        }

        protected override GameObject GetDefaultFocus() =>
            tabButtons is { Length: > 0 } ? tabButtons[0].gameObject : null;

        // ------------------------------------------------------------------ //
        // Tab selection
        // ------------------------------------------------------------------ //

        public void ClickTab(int index) => SelectTab(index);

        public void Close() => UIManager.Instance?.Pop();

        void SelectTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null)
                    tabPanels[i].SetActive(i == index);

            switch (index)
            {
                case 0: RefreshParcels();    break;
                case 1: RefreshStatistics(); break;
                case 2: RefreshRecords();    break;
            }
        }

        void RefreshAll()
        {
            RefreshParcels();
            if (_activeTab == 1) RefreshStatistics();
            else if (_activeTab == 2) RefreshRecords();
        }

        // ------------------------------------------------------------------ //
        // Parcels tab
        // ------------------------------------------------------------------ //

        void RefreshParcels()
        {
            if (parcelCards == null) return;
            for (int i = 0; i < parcelCards.Length; i++)
                parcelCards[i]?.Refresh(i);
        }

        // ------------------------------------------------------------------ //
        // Statistics tab
        // ------------------------------------------------------------------ //

        void RefreshStatistics()
        {
            var ss = SessionState.Instance;
            var p  = ss?.GetPlayer(0);

            SetText(statAreaCut,      $"{(p?.AreaCutCells ?? 0) * CELL_AREA_M2:F0} m²");
            SetText(statHayCollected, $"{p?.HayPilesCollected ?? 0}");
            SetText(statSquareBales,  $"{p?.SquareBalesMade ?? 0}");
            SetText(statRoundBales,   $"{p?.RoundBalesMade ?? 0}");
            SetText(statMoneyEarned,  $"${p?.MoneyEarned ?? 0}");
            SetText(statMoneySpent,   $"${p?.MoneySpent ?? 0}");
            SetText(statDistance,     $"{p?.DistanceTravelledM ?? 0:F0} m");
            SetText(statSwings,       $"{p?.TotalSwings ?? 0}");
            SetText(statPlaytime,     FormatTime(p?.PlaytimeSeconds ?? 0f));

            // Session-wide
            int wallet = CurrencyManager.Instance?.Money ?? 0;
            SetText(statWallet, $"${wallet}");

            int completed   = Fields.Core.WorldBootstrap.Instance?.CompletedParcels ?? 0;
            int totalParcels = Fields.Core.WorldBootstrap.Instance?.ActiveParcelCount ?? 1;
            SetText(statParcelsCompleted, $"{completed} / {totalParcels}");
            SetText(statTotalPlaytime,    FormatTime(ss?.TotalPlaytime ?? 0f));

            // Overall completion — read from the active GrassField (single terrain) or sum parcels with data.
            float overallPct = 0f;
            var activeField = Fields.UI.HUDController.Instance?.activeGrassField;
            if (activeField != null)
            {
                overallPct = activeField.GetCompletionPercent();
            }
            else
            {
                long cutTotal = 0, areaTotal = 0;
                for (int i = 0; i < 4; i++)
                {
                    var parcel = ss?.GetParcel(i);
                    if (parcel == null || parcel.AreaTotalCells == 0) continue;
                    cutTotal  += parcel.AreaCutCells;
                    areaTotal += parcel.AreaTotalCells;
                }
                if (areaTotal > 0) overallPct = (float)cutTotal / areaTotal * 100f;
            }
            SetText(statOverallCompletion, $"{overallPct:F1}%");
        }

        // ------------------------------------------------------------------ //
        // Records tab
        // ------------------------------------------------------------------ //

        void RefreshRecords()
        {
            var rec = Fields.Core.RecordsManager.Instance?.Data;

            SetText(recFastestParcel0, FormatParcelTime(rec?.fastestParcelSeconds[0] ?? -1f));
            SetText(recFastestParcel1, FormatParcelTime(rec?.fastestParcelSeconds[1] ?? -1f));
            SetText(recFastestParcel2, FormatParcelTime(rec?.fastestParcelSeconds[2] ?? -1f));
            SetText(recFastestParcel3, FormatParcelTime(rec?.fastestParcelSeconds[3] ?? -1f));

            double areaM2 = rec?.largestAreaCutM2 ?? 0.0;
            SetText(recLargestArea, areaM2 > 0.0 ? $"{areaM2:F0} m²" : "--");

            int bales = rec?.mostBalesDelivered ?? 0;
            SetText(recMostBales, bales > 0 ? $"{bales}" : "--");

            float streak = rec?.longestCuttingStreakSeconds ?? 0f;
            SetText(recLongestStreak, streak > 0f ? FormatTime(streak) : "--");

            float fullGame = rec?.fullGameCompletionSeconds ?? -1f;
            SetText(recFullGame, fullGame >= 0f ? FormatTime(fullGame) : "--");
        }

        static string FormatParcelTime(float seconds) =>
            seconds >= 0f ? FormatTime(seconds) : "--";

        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        static void SetText(TextMeshProUGUI field, string value)
        {
            if (field != null) field.text = value;
        }

        static string FormatTime(float seconds)
        {
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            return h > 0 ? $"{h}h {m:D2}m" : $"{m}m {s:D2}s";
        }
    }
}
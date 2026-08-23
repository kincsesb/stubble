using Fields.Core;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

        [Header("Statistics tab — per player (optional: assign or leave null for auto-build)")]
        public TextMeshProUGUI statAreaCut;
        public TextMeshProUGUI statHayCollected;
        public TextMeshProUGUI statSquareBales;
        public TextMeshProUGUI statRoundBales;
        public TextMeshProUGUI statMoneyEarned;
        public TextMeshProUGUI statMoneySpent;
        public TextMeshProUGUI statDistance;
        public TextMeshProUGUI statSwings;
        public TextMeshProUGUI statPlaytime;

        [Header("Statistics tab — session-wide (optional)")]
        public TextMeshProUGUI statWallet;
        public TextMeshProUGUI statParcelsCompleted;
        public TextMeshProUGUI statOverallCompletion;
        public TextMeshProUGUI statTotalPlaytime;

        [Header("Dynamic content parents (assign ScrollView Content if Inspector fields above are null)")]
        public Transform statisticsContent;
        public Transform recordsContent;

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

        public void ClickTab(int index) { Fields.Audio.UISoundManager.Click(); SelectTab(index); }

        public void Close() { Fields.Audio.UISoundManager.Click(); UIManager.Instance?.Pop(); }

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
        // Parcels tab — single aggregated card
        // ------------------------------------------------------------------ //

        void RefreshParcels()
        {
            if (parcelCards == null || parcelCards.Length == 0) return;

            var ss = SessionState.Instance;
            int activeCount = Fields.Core.WorldBootstrap.Instance?.ActiveParcelCount ?? 1;

            // Aggregate all parcel data into one combined view
            long totalAreaCut = 0, totalAreaTotal = 0;
            long totalHaySpawned = 0, totalHayCollected = 0, totalHayInField = 0;
            long totalSquareBales = 0, totalRoundBales = 0;
            long totalMoneyEarned = 0;
            float totalTime = 0f;

            for (int i = 0; i < activeCount; i++)
            {
                var p = ss?.GetParcel(i);
                if (p == null) continue;
                totalAreaCut      += p.AreaCutCells;
                totalAreaTotal    += p.AreaTotalCells;
                totalHaySpawned   += p.HayPilesSpawned;
                totalHayCollected += p.HayPilesCollected;
                totalHayInField   += p.HayPilesInField;
                totalSquareBales  += p.SquareBalesMade;
                totalRoundBales   += p.RoundBalesMade;
                totalMoneyEarned  += p.MoneyEarned;
                totalTime         += p.TimeSpentSeconds;
            }

            // Completion: authoritative from active GrassField
            float completionPct = 0f;
            var activeField = Fields.UI.HUDController.Instance?.activeGrassField;
            if (activeField != null)
                completionPct = activeField.GetCompletionPercent();
            else if (totalAreaTotal > 0)
                completionPct = (float)totalAreaCut / totalAreaTotal * 100f;

            string name = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get("journal.all_fields")
                : "Field";

            // Show only card 0 with aggregated data; hide the rest
            if (parcelCards[0] != null)
            {
                parcelCards[0].gameObject.SetActive(true);
                parcelCards[0].RefreshAggregated(name, completionPct,
                    totalAreaCut, totalAreaTotal,
                    totalHaySpawned, totalHayCollected, totalHayInField,
                    totalSquareBales, totalRoundBales,
                    totalMoneyEarned, totalTime);
            }
            for (int i = 1; i < parcelCards.Length; i++)
                if (parcelCards[i] != null) parcelCards[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        // Statistics tab
        // ------------------------------------------------------------------ //

        void RefreshStatistics()
        {
            // Dynamic content: StatsRenderer handles single-player and co-op uniformly.
            StatsRenderer.Build(statisticsContent);

            // ── Backward-compat: update any Inspector-assigned fixed TMP fields ── //
            var ss = SessionState.Instance;
            var p  = ss?.GetPlayer(0);
            int wallet    = Fields.Economy.CurrencyManager.Instance?.Money ?? 0;
            int completed = Fields.Core.WorldBootstrap.Instance?.CompletedParcels ?? 0;
            int total     = Fields.Core.WorldBootstrap.Instance?.ActiveParcelCount ?? 1;
            float pct     = HUDController.Instance?.activeGrassField?.GetCompletionPercent() ?? 0f;

            SetText(statAreaCut,           $"{(p?.AreaCutCells ?? 0) * CELL_AREA_M2:F0} m2");
            SetText(statHayCollected,      $"{p?.HayPilesCollected ?? 0}");
            SetText(statSquareBales,       $"{p?.SquareBalesMade ?? 0}");
            SetText(statRoundBales,        $"{p?.RoundBalesMade ?? 0}");
            var loc = Fields.Core.LocalizationManager.Instance;
            SetText(statMoneyEarned,       loc != null ? loc.FormatMoney(p?.MoneyEarned ?? 0) : $"${p?.MoneyEarned ?? 0}");
            SetText(statMoneySpent,        loc != null ? loc.FormatMoney(p?.MoneySpent ?? 0) : $"${p?.MoneySpent ?? 0}");
            SetText(statDistance,          $"{p?.DistanceTravelledM ?? 0:F0} m");
            SetText(statSwings,            $"{p?.TotalSwings ?? 0}");
            SetText(statPlaytime,          FormatTime(p?.PlaytimeSeconds ?? 0f));
            SetText(statWallet,            loc != null ? loc.FormatMoney(wallet) : $"${wallet}");
            SetText(statParcelsCompleted,  $"{completed} / {total}");
            SetText(statTotalPlaytime,     FormatTime(ss?.TotalPlaytime ?? 0f));
            SetText(statOverallCompletion, $"{pct:F1}%");
        }

        // ------------------------------------------------------------------ //
        // Records tab
        // ------------------------------------------------------------------ //

        void RefreshRecords()
        {
            var rec = Fields.Core.RecordsManager.Instance?.Data;
            int activeCount = Fields.Core.WorldBootstrap.Instance?.ActiveParcelCount ?? 4;

            // Update fixed Inspector-assigned TMP fields
            TextMeshProUGUI[] fastestFields = { recFastestParcel0, recFastestParcel1, recFastestParcel2, recFastestParcel3 };
            for (int i = 0; i < fastestFields.Length; i++)
            {
                if (fastestFields[i] == null) continue;
                fastestFields[i].transform.parent?.gameObject.SetActive(i < activeCount);
                if (i < activeCount)
                    SetText(fastestFields[i], FormatParcelTime(rec?.fastestParcelSeconds[i] ?? -1f));
            }

            double areaM2 = rec?.largestAreaCutM2 ?? 0.0;
            SetText(recLargestArea,   areaM2 > 0.0 ? $"{areaM2:F0} m²" : "--");

            int bales = rec?.mostBalesDelivered ?? 0;
            SetText(recMostBales, bales > 0 ? $"{bales}" : "--");

            float streak = rec?.longestCuttingStreakSeconds ?? 0f;
            SetText(recLongestStreak, streak > 0f ? FormatTime(streak) : "--");

            float fullGame = rec?.fullGameCompletionSeconds ?? -1f;
            SetText(recFullGame, fullGame >= 0f ? FormatTime(fullGame) : "--");

            // ── Dynamic fallback ── //
            if (recordsContent == null) return;
            ClearChildren(recordsContent);

            string Loc(string k) => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(k) : k;

            AddSectionHeader(recordsContent, Loc("journal.rec.sec.parcels"));
            for (int i = 0; i < activeCount; i++)
            {
                string parcelName = Loc($"journal.rec.fastest{i}");
                string time = FormatParcelTime(rec?.fastestParcelSeconds[i] ?? -1f);
                AddStatRow(recordsContent, parcelName, time);
            }

            AddSectionHeader(recordsContent, Loc("journal.rec.sec.session"));
            double aM2 = rec?.largestAreaCutM2 ?? 0.0;
            AddStatRow(recordsContent, Loc("journal.rec.label.largest"), aM2 > 0 ? $"{aM2:F0} m²" : "--");
            AddStatRow(recordsContent, Loc("journal.rec.label.bales"), (rec?.mostBalesDelivered ?? 0) > 0 ? $"{rec.mostBalesDelivered}" : "--");
            float strk = rec?.longestCuttingStreakSeconds ?? 0f;
            AddStatRow(recordsContent, Loc("journal.rec.label.streak"), strk > 0f ? FormatTime(strk) : "--");

            AddSectionHeader(recordsContent, Loc("journal.rec.sec.fullgame"));
            float fg = rec?.fullGameCompletionSeconds ?? -1f;
            AddStatRow(recordsContent, Loc("journal.rec.label.time"), fg >= 0f ? FormatTime(fg) : "--");
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

        // ── Dynamic row builders ── //

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void AddStatRow(Transform parent, string label, string value)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 2, 2);
            hlg.childForceExpandWidth = true;

            MakeLabel(row.transform, label, TextAlignmentOptions.Left,  new Color(0.75f, 0.75f, 0.75f));
            MakeLabel(row.transform, value, TextAlignmentOptions.Right, Color.white);
        }

        static void AddSectionHeader(Transform parent, string title)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 26);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 13;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = new Color(0.4f, 0.85f, 0.5f);
            tmp.alignment = TextAlignmentOptions.Left;
        }

        static void AddSectionDivider(Transform parent)
        {
            var go = new GameObject("Div", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 8);
        }

        static void MakeLabel(Transform parent, string text, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
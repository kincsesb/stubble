using Fields.Core;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Shared stat list builder used by JournalScreen (Statistics tab) and EndScreen.
    /// Clears container children and rebuilds a vertical list of sections + rows.
    /// Works for 1–4 players; highlights the local player in co-op.
    /// </summary>
    public static class StatsRenderer
    {
        const float CELL_AREA_M2 = 0.16f;

        // Row heights
        const float ROW_H    = 26f;
        const float HEADER_H = 30f;
        const float DIV_H    = 10f;

        // ------------------------------------------------------------------ //

        public static void Build(Transform container)
        {
            if (container == null) return;
            Clear(container);

            var ss  = SessionState.Instance;
            var cm  = CurrencyManager.Instance;

            float overallPct = HUDController.Instance?.activeGrassField?.GetCompletionPercent() ?? 0f;

            // ── Session summary ─────────────────────────────────────────── //
            AddHeader(container, L("journal.sec.session", "Session"));
            AddRow(container, L("journal.label.wallet",      "Wallet"),     FormatMoney(cm?.Money ?? 0));
            AddRow(container, L("journal.label.overall",     "Completion"), $"{overallPct:F0}%");
            AddRow(container, L("journal.label.totaltime",   "Total time"), FormatTime(ss?.TotalPlaytime ?? 0f));
            AddDiv(container);

            // ── Per-player ──────────────────────────────────────────────── //
            int count = ss?.PlayerCount ?? 1;
            bool multi = count > 1;

            for (int i = 0; i < count; i++)
            {
                var p = ss?.GetPlayer(i);
                if (p == null) continue;

                bool isLocal = ss.LocalPlayerId == i;
                var loc = LocalizationManager.Instance;
                string header = multi
                    ? (isLocal
                        ? (loc?.Get("journal.sec.player_you", i + 1) ?? $"Player {i + 1}  (You)")
                        : (loc?.Get("journal.sec.player",     i + 1) ?? $"Player {i + 1}"))
                    : L("journal.sec.your_stats", "Your Stats");

                AddHeader(container, header, isLocal ? new Color(0.32f, 0.47f, 0.25f) : new Color(0.25f, 0.40f, 0.60f));

                AddRow(container,
                    L("journal.label.areacut",     "Grass cut"),
                    $"{p.AreaCutCells * CELL_AREA_M2:N0} m²");   // m² — superscript 2 is Latin Extended, safe

                AddRow(container,
                    L("journal.label.swings",      "Swings"),
                    $"{p.TotalSwings:N0}");

                AddRow(container,
                    L("journal.label.distance",    "Distance"),
                    $"{p.DistanceTravelledM / 1000.0:F1} km");

                long bales = p.SquareBalesMade + p.RoundBalesMade;
                AddRow(container,
                    L("journal.label.bales",       "Bales made"),
                    $"{bales}");

                AddRow(container,
                    L("journal.label.earned",      "Earned"),
                    FormatMoney(p.MoneyEarned));

                AddRow(container,
                    L("journal.label.playtime",    "Playtime"),
                    FormatTime(p.PlaytimeSeconds));

                if (i < count - 1) AddDiv(container);
            }
        }

        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        static string L(string key, string fallback)
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return fallback;
            string v = loc.Get(key);
            return string.IsNullOrEmpty(v) || v == key ? fallback : v;
        }

        static string FormatMoney(long usd)
        {
            var loc = LocalizationManager.Instance;
            return loc != null ? loc.FormatMoney(usd) : $"${usd}";
        }

        static string FormatTime(float seconds)
        {
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            return h > 0 ? $"{h}h {m:D2}m" : $"{m}m {s:D2}s";
        }

        static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void AddHeader(Transform parent, string title,
                              Color? color = null)
        {
            var go = new GameObject("H", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, HEADER_H);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = HEADER_H;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = title.ToUpper();
            tmp.fontSize  = 11;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = color ?? new Color(0.32f, 0.47f, 0.25f);  // olive green
            tmp.alignment = TextAlignmentOptions.BottomLeft;
        }

        static void AddRow(Transform parent, string label, string value)
        {
            var row = new GameObject("R", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, ROW_H);

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = ROW_H;

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding              = new RectOffset(4, 4, 2, 2);
            hlg.childForceExpandWidth = true;

            MakeTMP(row.transform, label, TextAlignmentOptions.Left,  new Color(0.40f, 0.32f, 0.22f), 13);  // medium ink
            MakeTMP(row.transform, value, TextAlignmentOptions.Right, new Color(0.18f, 0.12f, 0.08f), 13);  // dark ink
        }

        static void AddDiv(Transform parent)
        {
            var go = new GameObject("D", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = DIV_H;

            // Thin horizontal line
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.42f, 0.28f, 0.30f);  // warm brown divider
        }

        static void MakeTMP(Transform parent, string text, TextAlignmentOptions align, Color color, float size)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text            = text;
            tmp.fontSize        = size;
            tmp.color           = color;
            tmp.alignment       = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode    = TextOverflowModes.Ellipsis;
        }
    }
}

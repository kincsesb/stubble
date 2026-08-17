using System.Collections.Generic;
using Fields.Core;
using Fields.Economy;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// End-game stats panel shown after nuclear or peaceful completion.
    /// Builds its content entirely at runtime so no prefab changes are needed:
    ///   • Stats row   — time, money, cut area
    ///   • Line graph  — time (X) vs cut area (Y) per player, drawn to Texture2D
    ///   • Legend      — player colour + Steam name
    ///   • Achievements list
    ///   • Play Again / Quit buttons
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        public enum EndingType { Peaceful, Loop, Nuclear }
        public static EndingType PendingEndingType = EndingType.Peaceful;

        // ── Inspector refs (scene-placed elements reused / repositioned) ─── //
        [Header("Required scene refs")]
        public TextMeshProUGUI titleText;
        public Button          playAgainButton;
        public Button          quitButton;

        [Header("Optional legacy refs (unused when building dynamic UI)")]
        public TextMeshProUGUI totalEarningsText;
        public TextMeshProUGUI timePlayedText;
        public TextMeshProUGUI grassCutText;
        public TextMeshProUGUI balesMadeText;
        public TextMeshProUGUI totalSwingsText;
        public TextMeshProUGUI distanceTravelledText;
        public TextMeshProUGUI commentText;
        public RectTransform   statsContent;

        // ── Graph visual settings ─────────────────────────────────────────── //
        [Header("Graph")]
        public int   graphTexWidth  = 512;
        public int   graphTexHeight = 200;

        // ── Colours per player index ──────────────────────────────────────── //
        static readonly Color[] PlayerColors =
        {
            new Color(0.2f, 0.8f, 1.0f),   // cyan
            new Color(1.0f, 0.55f, 0.1f),  // orange
            new Color(0.3f, 1.0f, 0.4f),   // green
            new Color(1.0f, 0.3f, 0.9f),   // magenta
        };

        const float CELL_SIZE_M = 0.4f;

        static float _gameStartTime;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RecordStartTime() => _gameStartTime = Time.realtimeSinceStartup;

        // ── Runtime ───────────────────────────────────────────────────────── //
        readonly List<GameObject> _dynamicChildren = new List<GameObject>();

        void OnEnable()
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            BuildDynamicUI();

            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveAllListeners();
                playAgainButton.onClick.AddListener(GoToMainMenu);
                var btnLabel = playAgainButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnLabel != null)
                {
                    var loc2 = LocalizationManager.Instance;
                    btnLabel.text = loc2 != null ? loc2.Get("end.button.mainmenu") : "Main Menu";
                }
            }
            if (quitButton != null)
                quitButton.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            foreach (var go in _dynamicChildren)
                if (go != null) Destroy(go);
            _dynamicChildren.Clear();
        }

        // ======================================================================
        // UI construction
        // ======================================================================

        void BuildDynamicUI()
        {
            var ss  = SessionState.Instance;
            var loc = LocalizationManager.Instance;
            var cur = CurrencyManager.Instance;
            var sm  = SteamManager.Instance;

            // ── Raw values ────────────────────────────────────────────────── //
            float elapsed  = Time.realtimeSinceStartup - _gameStartTime;
            int   mins     = Mathf.FloorToInt(elapsed / 60f);
            int   secs     = Mathf.FloorToInt(elapsed % 60f);
            int   money    = cur  != null ? cur.Money : 0;
            int   players  = ss   != null ? ss.PlayerCount : 1;

            long  totalCut  = 0;
            if (ss != null)
                foreach (var p in ss.Players)
                    if (p != null) totalCut += p.AreaCutCells;
            float cutM2 = totalCut * CELL_SIZE_M * CELL_SIZE_M;

            // ── Title ─────────────────────────────────────────────────────── //
            MoveAnchor(titleText?.rectTransform, 0.03f, 0.88f, 0.97f, 0.97f);
            if (titleText != null)
            {
                titleText.text = PendingEndingType == EndingType.Nuclear
                    ? (loc != null ? loc.Get("end.title.nuclear") : "☢ Nuclear Ending")
                    : (loc != null ? loc.Get("end.title") : "All Fields Cleared!");
            }

            // ── Find the panel to host dynamic elements ───────────────────── //
            Transform panel = transform; // EndScreen_Canvas itself
            if (transform.childCount > 0) panel = transform.GetChild(0); // EndPanel

            // ── Stats row (time | money | grass) ─────────────────────────── //
            AddStatsRow(panel, mins, secs, money, cutM2, loc);

            // ── Graph + Legend ────────────────────────────────────────────── //
            AddGraph(panel, players, ss);
            AddLegend(panel, players, sm);

            // ── Achievements ──────────────────────────────────────────────── //
            AddAchievements(panel, sm);

            // ── Reposition single button to bottom centre ─────────────────── //
            MoveAnchor(playAgainButton?.GetComponent<RectTransform>(), 0.25f, 0.04f, 0.75f, 0.13f);
        }

        // ── Stats row ─────────────────────────────────────────────────────── //

        void AddStatsRow(Transform parent, int mins, int secs, int money, float cutM2, LocalizationManager loc)
        {
            string timeStr  = $"{mins:00}:{secs:00}";
            string moneyStr = loc != null ? loc.FormatMoney(money) : $"${money}";
            string areaStr  = $"{cutM2:N0}";

            string timeFmt  = loc != null ? loc.Get("end.stat.time",  timeStr)  : "⏱ " + timeStr;
            string moneyFmt = loc != null ? loc.Get("end.stat.money", moneyStr) : "💰 " + moneyStr;
            string areaFmt  = loc != null ? loc.Get("end.stat.area",  areaStr)  : "🌿 " + areaStr + " m²";

            float yMin = 0.76f, yMax = 0.86f;

            // Three equal-width columns
            AddStatCell(parent, "StatTime",  timeFmt,  0.03f, yMin, 0.35f, yMax);
            AddStatCell(parent, "StatMoney", moneyFmt, 0.36f, yMin, 0.64f, yMax);
            AddStatCell(parent, "StatGrass", areaFmt,  0.65f, yMin, 0.97f, yMax);

            // Hide old text refs if they're wired (they'd overlap)
            HideIfNotNull(totalEarningsText?.gameObject);
            HideIfNotNull(timePlayedText?.gameObject);
            HideIfNotNull(grassCutText?.gameObject);
            HideIfNotNull(balesMadeText?.gameObject);
            HideIfNotNull(totalSwingsText?.gameObject);
            HideIfNotNull(distanceTravelledText?.gameObject);
            HideIfNotNull(commentText?.gameObject);
        }

        void AddStatCell(Transform parent, string goName, string text, float xMin, float yMin, float xMax, float yMax)
        {
            var go = CreateRectChild(parent, goName);
            _dynamicChildren.Add(go);
            SetAnchors(go.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);

            var img = go.AddComponent<RawImage>();
            img.color = new Color(0f, 0f, 0f, 0.35f);

            var textGO = CreateRectChild(go.transform, "Label");
            SetAnchors(textGO.GetComponent<RectTransform>(), 0.05f, 0.1f, 0.95f, 0.9f);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
        }

        // ── Graph ─────────────────────────────────────────────────────────── //

        void AddGraph(Transform parent, int playerCount, SessionState ss)
        {
            var tracker = PlayerGraphTracker.Instance;
            if (tracker == null) return;

            // ── Header ────────────────────────────────────────────────────── //
            var headerGO = CreateRectChild(parent, "GraphHeader");
            _dynamicChildren.Add(headerGO);
            SetAnchors(headerGO.GetComponent<RectTransform>(), 0.03f, 0.70f, 0.97f, 0.75f);
            var hTmp = headerGO.AddComponent<TextMeshProUGUI>();
            var locG = LocalizationManager.Instance;
            hTmp.text      = locG != null ? locG.Get("end.graph.header") : "CUT AREA OVER TIME";
            hTmp.fontSize  = 16;
            hTmp.fontStyle = FontStyles.Bold;
            hTmp.color     = new Color(0.85f, 0.85f, 0.85f);
            hTmp.alignment = TextAlignmentOptions.Left;

            // ── Graph image ───────────────────────────────────────────────── //
            var graphGO = CreateRectChild(parent, "GraphImage");
            _dynamicChildren.Add(graphGO);
            SetAnchors(graphGO.GetComponent<RectTransform>(), 0.03f, 0.42f, 0.97f, 0.69f);

            var raw = graphGO.AddComponent<RawImage>();
            raw.texture = DrawGraph(tracker, playerCount, ss);

            // Y-axis label
            var yLabelGO = CreateRectChild(parent, "GraphYLabel");
            _dynamicChildren.Add(yLabelGO);
            var yrt = yLabelGO.GetComponent<RectTransform>();
            SetAnchors(yrt, 0.0f, 0.42f, 0.04f, 0.69f);
            var yTmp = yLabelGO.AddComponent<TextMeshProUGUI>();
            var locY = LocalizationManager.Instance;
            yTmp.text      = locY != null ? locY.Get("end.graph.yaxis") : "m²";
            yTmp.fontSize  = 12;
            yTmp.color     = new Color(0.7f, 0.7f, 0.7f);
            yTmp.alignment = TextAlignmentOptions.Center;

            // X-axis label
            var xLabelGO = CreateRectChild(parent, "GraphXLabel");
            _dynamicChildren.Add(xLabelGO);
            SetAnchors(xLabelGO.GetComponent<RectTransform>(), 0.03f, 0.39f, 0.97f, 0.43f);
            var xTmp = xLabelGO.AddComponent<TextMeshProUGUI>();
            var locX = LocalizationManager.Instance;
            xTmp.text      = locX != null ? locX.Get("end.graph.xaxis") : "Time (min)";
            xTmp.fontSize  = 12;
            xTmp.color     = new Color(0.7f, 0.7f, 0.7f);
            xTmp.alignment = TextAlignmentOptions.Right;
        }

        Texture2D DrawGraph(PlayerGraphTracker tracker, int playerCount, SessionState ss)
        {
            int w = graphTexWidth, h = graphTexHeight;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // Background
            Color bg = new Color(0.08f, 0.08f, 0.12f, 1f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Axis lines
            Color axisCol = new Color(0.4f, 0.4f, 0.4f, 1f);
            int padL = 35, padB = 20, padR = 10, padT = 10;
            DrawHLine(pixels, w, padB, padL, w - padR, axisCol);
            DrawVLine(pixels, w, padL, padB, h - padT, axisCol);

            // Find global max values for scaling
            float maxTime = 1f, maxArea = 1f;
            for (int pi = 0; pi < playerCount; pi++)
            {
                var samples = tracker.GetSamples(pi);
                foreach (var s in samples)
                {
                    if (s.TimeSec > maxTime) maxTime = s.TimeSec;
                    if (s.AreaM2  > maxArea) maxArea  = s.AreaM2;
                }
            }

            // Grid lines (light)
            Color gridCol = new Color(0.2f, 0.2f, 0.25f, 1f);
            for (int g = 1; g <= 4; g++)
            {
                int gy = padB + Mathf.RoundToInt((h - padT - padB) * g / 4f);
                int gx = padL + Mathf.RoundToInt((w - padR - padL) * g / 4f);
                DrawHLine(pixels, w, gy, padL, w - padR, gridCol);
                DrawVLine(pixels, w, gx, padB, h - padT, gridCol);
            }

            // Plot per-player lines
            for (int pi = 0; pi < playerCount; pi++)
            {
                var samples = tracker.GetSamples(pi);
                if (samples.Length < 2) continue;
                Color lineCol = PlayerColors[pi % PlayerColors.Length];

                for (int si = 1; si < samples.Length; si++)
                {
                    float x0 = Remap(samples[si-1].TimeSec, 0, maxTime, padL, w - padR);
                    float y0 = Remap(samples[si-1].AreaM2,  0, maxArea, padB, h - padT);
                    float x1 = Remap(samples[si].TimeSec,   0, maxTime, padL, w - padR);
                    float y1 = Remap(samples[si].AreaM2,    0, maxArea, padB, h - padT);
                    DrawLine(pixels, w, h, Mathf.RoundToInt(x0), Mathf.RoundToInt(y0),
                                          Mathf.RoundToInt(x1), Mathf.RoundToInt(y1), lineCol);
                }
            }

            // Y-axis tick labels (0, max/2, max)
            // (left as overlay TextMeshPro for now — pure texture labels are complex)

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ── Legend ────────────────────────────────────────────────────────── //

        void AddLegend(Transform parent, int playerCount, SteamManager sm)
        {
            var legendGO = CreateRectChild(parent, "Legend");
            _dynamicChildren.Add(legendGO);
            SetAnchors(legendGO.GetComponent<RectTransform>(), 0.03f, 0.36f, 0.97f, 0.41f);

            float colWidth = 1f / Mathf.Max(1, playerCount);
            for (int pi = 0; pi < playerCount; pi++)
            {
                string name = pi == 0
                    ? (sm != null ? sm.LocalPlayerName : "Player 1")
                    : $"Player {pi + 1}";
                Color col = PlayerColors[pi % PlayerColors.Length];
                float xMin = pi * colWidth + 0.01f;
                float xMax = (pi + 1) * colWidth - 0.01f;

                var entryGO = CreateRectChild(legendGO.transform, $"LegendEntry{pi}");
                SetAnchors(entryGO.GetComponent<RectTransform>(), xMin, 0f, xMax, 1f);

                var swatch = CreateRectChild(entryGO.transform, "Swatch");
                SetAnchors(swatch.GetComponent<RectTransform>(), 0f, 0.2f, 0.12f, 0.8f);
                var swatchImg = swatch.AddComponent<RawImage>();
                swatchImg.color = col;

                var labelGO = CreateRectChild(entryGO.transform, "Label");
                SetAnchors(labelGO.GetComponent<RectTransform>(), 0.14f, 0f, 1f, 1f);
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.text      = name;
                tmp.fontSize  = 15;
                tmp.color     = col;
                tmp.alignment = TextAlignmentOptions.Left;
            }
        }

        // ── Achievements ──────────────────────────────────────────────────── //

        void AddAchievements(Transform parent, SteamManager sm)
        {
            var achGO = CreateRectChild(parent, "Achievements");
            _dynamicChildren.Add(achGO);
            SetAnchors(achGO.GetComponent<RectTransform>(), 0.03f, 0.15f, 0.97f, 0.35f);

            var headerGO = CreateRectChild(achGO.transform, "AchHeader");
            SetAnchors(headerGO.GetComponent<RectTransform>(), 0f, 0.78f, 1f, 1f);
            var hTmp = headerGO.AddComponent<TextMeshProUGUI>();
            var locA = LocalizationManager.Instance;
            hTmp.text      = locA != null ? locA.Get("end.ach.header") : "ACHIEVEMENTS THIS SESSION";
            hTmp.fontSize  = 16;
            hTmp.fontStyle = FontStyles.Bold;
            hTmp.color     = new Color(0.9f, 0.8f, 0.2f);
            hTmp.alignment = TextAlignmentOptions.Left;

            var listGO = CreateRectChild(achGO.transform, "AchList");
            SetAnchors(listGO.GetComponent<RectTransform>(), 0f, 0f, 1f, 0.75f);
            var listTmp = listGO.AddComponent<TextMeshProUGUI>();

            var achs = sm?.SessionAchievements;
            if (achs == null || achs.Count == 0)
            {
                var locEmpty = LocalizationManager.Instance;
                string emptyMsg = locEmpty != null ? locEmpty.Get("end.ach.empty") : "No achievements unlocked this session.";
                listTmp.text = $"<color=#888888>{emptyMsg}</color>";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (var id in achs)
                    sb.Append("• ").Append(FriendlyAchName(id)).Append("  ");
                listTmp.text = sb.ToString();
            }
            listTmp.fontSize        = 14;
            listTmp.color           = new Color(0.9f, 0.9f, 0.7f);
            listTmp.enableWordWrapping = true;
            listTmp.alignment       = TextAlignmentOptions.TopLeft;
        }

        static string FriendlyAchName(string id)
        {
            // Strip ACH_ prefix and titlecase
            string s = id.StartsWith("ACH_") ? id.Substring(4) : id;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                       .ToTitleCase(s.Replace('_', ' ').ToLower());
        }

        // ======================================================================
        // Helpers — UI construction
        // ======================================================================

        static GameObject CreateRectChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin  = new Vector2(xMin, yMin);
            rt.anchorMax  = new Vector2(xMax, yMax);
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }

        static void MoveAnchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            if (rt == null) return;
            SetAnchors(rt, xMin, yMin, xMax, yMax);
        }

        static void HideIfNotNull(GameObject go)
        {
            if (go != null) go.SetActive(false);
        }

        // ======================================================================
        // Helpers — Texture2D graph drawing
        // ======================================================================

        static void DrawHLine(Color[] pixels, int w, int y, int x0, int x1, Color col)
        {
            if (y < 0 || y >= pixels.Length / w) return;
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                pixels[y * w + x] = col;
        }

        static void DrawVLine(Color[] pixels, int w, int x, int y0, int y1, Color col)
        {
            int h = pixels.Length / w;
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
                pixels[y * w + x] = col;
        }

        static void DrawLine(Color[] pixels, int w, int h, int x0, int y0, int x1, int y1, Color col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                {
                    pixels[y0 * w + x0] = col;
                    // 2px thick
                    if (x0 + 1 < w) pixels[y0 * w + x0 + 1] = col;
                    if (y0 + 1 < h) pixels[(y0 + 1) * w + x0] = col;
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        static float Remap(float val, float fromMin, float fromMax, float toMin, float toMax)
        {
            if (Mathf.Approximately(fromMax, fromMin)) return toMin;
            return toMin + (val - fromMin) / (fromMax - fromMin) * (toMax - toMin);
        }

        // ======================================================================
        // Button actions
        // ======================================================================

        void GoToMainMenu()
        {
            Time.timeScale = 1f;
            string sceneName = SceneManager.GetActiveScene().name;
            if (NetworkManager.singleton != null)
                NetworkManager.singleton.ServerChangeScene(sceneName);
            else
                SceneManager.LoadScene(sceneName);
        }
    }
}

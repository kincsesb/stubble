using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fields.World
{
    /// <summary>
    /// Hozzáadandó a vintage_computer_3d_model objektumhoz.
    /// A játékos ránézve [E]-t nyom -> megjelenik a retró DOS-stílusú terminál.
    /// Bekötés: az objektumnak kell egy Collider is a raycast-hez (BoxCollider ajánlott).
    /// CheatCodeActivator-t automatikusan létrehozza, ha nincs a scene-ben.
    /// </summary>
    public class CheatCodeTerminal : MonoBehaviour, Fields.Core.IInteractable
    {
        // ── Colors ──────────────────────────────────────────────────────── //
        static readonly Color BG_PANEL   = new(0.02f, 0.06f, 0.02f, 0.97f);
        static readonly Color BG_DIMMER  = new(0.00f, 0.00f, 0.00f, 0.78f);
        static readonly Color BG_BORDER  = new(0.05f, 0.18f, 0.05f, 1.00f);
        static readonly Color BG_INPUT   = new(0.01f, 0.03f, 0.01f, 1.00f);
        static readonly Color C_GREEN    = new(0.12f, 0.92f, 0.28f, 1.00f);
        static readonly Color C_DIM      = new(0.05f, 0.40f, 0.12f, 1.00f);
        static readonly Color C_AMBER    = new(0.95f, 0.78f, 0.05f, 1.00f);
        static readonly Color C_RED      = new(0.90f, 0.22f, 0.12f, 1.00f);
        static readonly Color C_CYAN     = new(0.20f, 0.85f, 0.85f, 1.00f);

        // ── ASCII headers (const English — intentional retro aesthetic) ── //
        const string HEADER_CHEAT =
            "+--------------------------------------------------+\n" +
            "| STUBBLE OS v1.0   (C) 1987 BARNYARD SYSTEMS INC |\n" +
            "| ** CHEATING IS IMMORAL. DO IT ANYWAY. **         |\n" +
            "+--------------------------------------------------+\n" +
            "  TYPE YOUR CHEAT CODE AND PRESS [ENTER]\n" +
            "  TYPE 'AI'   TO TALK TO BARNYARD A.I.\n" +
            "  TYPE 'SNAKE' FOR A SURPRISE\n" +
            "  [ESC] TO EXIT LIKE A COWARD\n" +
            "--------------------------------------------------";

        const string HEADER_AI =
            "+--------------------------------------------------+\n" +
            "|    BARNYARD A.I. v0.1  --  EST. HARVEST 1987    |\n" +
            "| TRAINED ON 47 BOOKS AND ONE FARMING ALMANAC.    |\n" +
            "+--------------------------------------------------+\n" +
            "  TYPE 'HELP' FOR COMMANDS. TYPE 'BACK' TO FLEE.\n" +
            "--------------------------------------------------";

        const string PROMPT_CHEAT = "C:\\FARM> ";
        const string PROMPT_AI    = "> ";

        // Localization keys for escalating cheat failure messages
        static readonly string[] FAIL_KEYS =
        {
            "terminal.fail.0",
            "terminal.fail.1",
            "terminal.fail.2",
            "terminal.fail.3",
            "terminal.fail.4",
        };

        // ── Static state ─────────────────────────────────────────────────── //

        /// <summary>UIManager checks this to skip opening the pause menu while the terminal is open.</summary>
        public static bool IsAnyOpen { get; private set; }

        static CheatCodeTerminal _activeTerminal;

        /// <summary>UIManager calls this instead of opening the pause menu when ESC is pressed.</summary>
        public static void CloseIfOpen() => _activeTerminal?.CloseTerminal();

        // ── Runtime state ────────────────────────────────────────────────── //
        bool _isOpen;
        bool _aiMode;
        bool _snakeMode;
        string _currentInput = "";
        readonly List<(string text, bool ok)> _history = new();
        int _failCount;
        int _aiFallCount;

        // Cursor blink
        float _blinkTimer;
        bool _cursorVisible = true;

        // Delay keyboard subscription by one frame so the opening E-press is not captured
        bool _pendingSubscribe;

        // Snake state
        TerminalSnake _snake;
        float _snakeTickTimer;
        const float SNAKE_TICK_BASE = 0.18f;   // starting interval; speeds up with score

        // Scroll control: only snap to bottom when new content arrives
        bool _scrollDirty;

        // UI refs
        Canvas         _canvas;
        TextMeshProUGUI _mainText;
        ScrollRect     _scrollRect;
        RectTransform  _contentRT;

        // ── Lifecycle ────────────────────────────────────────────────────── //

        void Awake()
        {
            EnsureActivator();
            BuildUI();
            _canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            IsAnyOpen = false;
            UnsubscribeKeyboard();
        }

        void Update()
        {
            if (_pendingSubscribe)
            {
                _pendingSubscribe = false;
                if (Keyboard.current != null)
                    Keyboard.current.onTextInput += OnTextInput;
            }

            if (!_isOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // ── Snake mode input ─────────────────────────────────────────── //
            if (_snakeMode)
            {
                UpdateSnake(kb);
                return;
            }

            if (kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame)     { SubmitInput(); return; }
            if (kb.backspaceKey.wasPressedThisFrame && _currentInput.Length > 0)
            {
                _currentInput = _currentInput[..^1];
                RefreshDisplay();
            }

            // Cursor blink
            _blinkTimer += Time.unscaledDeltaTime;
            if (_blinkTimer >= 0.53f)
            {
                _blinkTimer = 0f;
                _cursorVisible = !_cursorVisible;
                RefreshDisplay();
            }
        }

        void UpdateSnake(Keyboard kb)
        {
            // Arrow key input
            if (kb.upArrowKey.wasPressedThisFrame)    _snake.SetDir(KeyCode.UpArrow);
            if (kb.downArrowKey.wasPressedThisFrame)  _snake.SetDir(KeyCode.DownArrow);
            if (kb.leftArrowKey.wasPressedThisFrame)  _snake.SetDir(KeyCode.LeftArrow);
            if (kb.rightArrowKey.wasPressedThisFrame) _snake.SetDir(KeyCode.RightArrow);

            // Q or Esc quits snake — ESC is handled by UIManager, Q handled here
            if (kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                ExitSnakeMode();
                return;
            }

            // Restart on Enter after game over
            if (!_snake.IsAlive && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                _snake = new TerminalSnake();
                _snakeTickTimer = 0f;
                RefreshSnake();
                return;
            }

            // Speed increases with score; carry over excess time for accurate cadence
            float tickInterval = Mathf.Max(0.07f, SNAKE_TICK_BASE - _snake.Score * 0.007f);
            _snakeTickTimer += Time.unscaledDeltaTime;
            bool ticked = false;
            while (_snakeTickTimer >= tickInterval)
            {
                _snakeTickTimer -= tickInterval;
                _snake.Tick();
                ticked = true;
                if (!_snake.IsAlive) break;
            }

            if (ticked) RefreshSnake();
        }

        // ── IInteractable ────────────────────────────────────────────────── //

        public void Interact(Fields.Core.PlayerController player)
        {
            if (_isOpen) return;
            OpenTerminal();
        }

        // ── Terminal open / close ────────────────────────────────────────── //

        void OpenTerminal()
        {
            _isOpen = true;
            IsAnyOpen = true;
            _activeTerminal = this;
            _canvas.gameObject.SetActive(true);
            Fields.Feel.GameFeelController.Instance?.TriggerTerminalFeel();
            _currentInput = "";
            _cursorVisible = true;
            _blinkTimer = 0f;

            var pc = Fields.Core.PlayerController.Instance;
            if (pc != null) pc.InputLocked = true;

            pc?.GetComponentInChildren<Fields.Tools.ToolHolder>(true)?.EquipBareHand();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _pendingSubscribe = true;
            _scrollDirty = true;
            RefreshDisplay();
        }

        void CloseTerminal()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsAnyOpen = false;
            _activeTerminal = null;
            _snakeMode = false;
            _aiMode    = false;
            _canvas.gameObject.SetActive(false);
            _pendingSubscribe = false;

            UnsubscribeKeyboard();

            var pc = Fields.Core.PlayerController.Instance;
            if (pc != null) pc.InputLocked = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ── Keyboard input ───────────────────────────────────────────────── //

        void UnsubscribeKeyboard()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;
        }

        void OnTextInput(char c)
        {
            if (!_isOpen || _snakeMode) return;
            if (c < 32 || c == 127) return;
            if (_currentInput.Length >= 24) return;
            _currentInput += c;
            RefreshDisplay();
        }

        // ── Submit ───────────────────────────────────────────────────────── //

        void SubmitInput()
        {
            string raw = _currentInput.Trim();
            _currentInput = "";

            if (string.IsNullOrEmpty(raw))
            {
                RefreshDisplay();
                return;
            }

            if (_aiMode)
                SubmitAi(raw);
            else
                SubmitCheat(raw);

            _history.Add(("", true));
            while (_history.Count > 80) _history.RemoveAt(0);
            _scrollDirty = true;
            RefreshDisplay();
        }

        void SubmitCheat(string raw)
        {
            _history.Add(($"{PROMPT_CHEAT}{raw.ToUpper()}", true));

            string lower = raw.Trim().ToLower();

            if (lower == "ai")    { EnterAiMode();    return; }
            if (lower == "snake") { EnterSnakeMode(); return; }

            var activator = Fields.Core.CheatCodeActivator.Instance;
            if (activator == null)
            {
                var loc0 = Fields.Core.LocalizationManager.Instance;
                string offline = loc0 != null ? loc0.Get("terminal.offline") : "[!!] CHEAT ENGINE OFFLINE. RESTART REQUIRED.";
                _history.Add((offline, false));
                return;
            }

            var (ok, response) = activator.TryActivate(raw);
            if (ok)
            {
                _failCount = 0;
                _history.Add(($"  [OK] {response}", true));
            }
            else
            {
                string failMsg = response ?? BuildFailMsg(raw);
                _history.Add(($"  [!!] {failMsg}", false));
                _failCount++;
            }
        }

        void SubmitAi(string raw)
        {
            _history.Add(($"{PROMPT_AI}{raw.ToUpper()}", true));

            var result = BarnAI.Query(raw);

            if (result.ExitAiMode)
            {
                ExitAiMode();
                return;
            }

            if (result.Text != null)
            {
                _aiFallCount = 0;
                // Multi-line responses: each line gets its own history entry
                foreach (var line in result.Text.Split('\n'))
                    _history.Add(($"  {line}", true));
                if (result.AchievementId != null)
                    Fields.Core.SteamManager.Instance?.UnlockAchievement(result.AchievementId);
            }
            else
            {
                string fallback = BarnAI.GetFallback(_aiFallCount, raw);
                _history.Add(($"  {fallback}", false));
                _aiFallCount++;
            }
        }

        void EnterAiMode()
        {
            _aiMode      = true;
            _aiFallCount = 0;
            _history.Clear();

            var loc = Fields.Core.LocalizationManager.Instance;
            string enterMsg = loc != null ? loc.Get("ai.enter") : "[BARNYARD A.I. v0.1] ACTIVATED.";
            _history.Add(($"  [OK] {enterMsg}", true));

            Fields.Core.SteamManager.Instance?.UnlockAchievement(
                Fields.Core.SteamManager.Achievements.MEET_AI);
        }

        void ExitAiMode()
        {
            _aiMode = false;
            _history.Clear();

            var loc = Fields.Core.LocalizationManager.Instance;
            string exitMsg = loc != null ? loc.Get("ai.exit") : "[EXITING A.I. MODE]";
            _history.Add(($"  [OK] {exitMsg}", true));
        }

        void EnterSnakeMode()
        {
            _snakeMode = true;
            _snake     = new TerminalSnake();
            _snakeTickTimer = 0f;
            UnsubscribeKeyboard();  // snake uses direct key polling, not text input
            RefreshSnake();
        }

        void ExitSnakeMode()
        {
            _snakeMode = false;
            _snake     = null;
            var loc = Fields.Core.LocalizationManager.Instance;
            string exitMsg = loc != null ? loc.Get("snake.exit") : "SNAKE EXITED. SCORE: IRRELEVANT. YOU ARE A FARMER.";
            _history.Add(($"  [OK] {exitMsg}", true));
            _history.Add(("", true));
            // Re-subscribe text input
            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;
            RefreshDisplay();
        }

        string BuildFailMsg(string raw)
        {
            int idx = Mathf.Min(_failCount, FAIL_KEYS.Length - 1);
            var loc = Fields.Core.LocalizationManager.Instance;
            string tpl = loc != null ? loc.Get(FAIL_KEYS[idx]) : "BAD COMMAND: {0}";
            return string.Format(tpl, raw.ToUpper());
        }

        // ── Display ──────────────────────────────────────────────────────── //

        void RefreshDisplay()
        {
            if (_mainText == null) return;

            string header = _aiMode ? HEADER_AI : HEADER_CHEAT;
            string prompt = _aiMode ? PROMPT_AI : PROMPT_CHEAT;

            var sb = new System.Text.StringBuilder();
            // Monospace tag so ASCII art borders align regardless of font proportions
            sb.Append("<mspace=0.58em>");
            sb.AppendLine(header);
            sb.AppendLine();

            foreach (var (text, ok) in _history)
            {
                if (string.IsNullOrEmpty(text)) { sb.AppendLine(); continue; }

                if (text.StartsWith(PROMPT_CHEAT) || text.StartsWith(PROMPT_AI))
                    sb.AppendLine($"<color=#{ColorHex(C_DIM)}>{text}</color>");
                else if (text.StartsWith("  [OK]"))
                    sb.AppendLine($"<color=#{ColorHex(C_AMBER)}>{text}</color>");
                else if (text.StartsWith("  [!!]"))
                    sb.AppendLine($"<color=#{ColorHex(C_RED)}>{text}</color>");
                else if (text.StartsWith("  >"))
                    sb.AppendLine($"<color=#{ColorHex(C_CYAN)}>{text}</color>");
                else
                    sb.AppendLine(text);
            }

            string cursor = _cursorVisible ? "_" : " ";
            sb.Append($"<color=#{ColorHex(C_GREEN)}>{prompt}{_currentInput.ToUpper()}{cursor}</color>");
            sb.Append("</mspace>");

            _mainText.text = sb.ToString();
            if (_scrollDirty) { ScrollToBottom(); _scrollDirty = false; }
        }

        void RefreshSnake()
        {
            if (_mainText == null || _snake == null) return;

            var loc   = Fields.Core.LocalizationManager.Instance;
            string hdr   = loc != null ? loc.Get("snake.header", _snake.Score) : $"  SNAKE  --  SCORE: {_snake.Score}  --  [Q] QUIT  --  ARROW KEYS";
            string over  = loc != null ? loc.Get("snake.gameover", _snake.Score) : $"[!!] GAME OVER. SCORE: {_snake.Score}. [ENTER] RESTART  [Q] QUIT";
            string start = loc != null ? loc.Get("snake.start") : "PRESS AN ARROW KEY TO START";

            var sb = new System.Text.StringBuilder();
            sb.Append("<mspace=0.58em>");
            sb.AppendLine(hdr);
            sb.AppendLine();
            sb.Append(_snake.Frame());

            if (!_snake.IsAlive)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append($"  <color=#{ColorHex(C_RED)}>{over}</color>");
            }
            else if (!_snake.HasStarted)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append($"  <color=#{ColorHex(C_AMBER)}>{start}</color>");
            }
            sb.Append("</mspace>");

            _mainText.text = sb.ToString();
        }

        void ScrollToBottom()
        {
            if (_scrollRect == null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRT);
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        static string ColorHex(Color c)
        {
            return $"{ToByte(c.r):X2}{ToByte(c.g):X2}{ToByte(c.b):X2}";
        }

        static int ToByte(float f) => Mathf.RoundToInt(Mathf.Clamp01(f) * 255f);

        // ── UI construction ──────────────────────────────────────────────── //

        void BuildUI()
        {
            // Root canvas
            var canvasGO = new GameObject("CheatTerminalCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer
            var dimmer = MakeImage(canvasGO.transform, "Dimmer", BG_DIMMER);
            StretchFull(dimmer);

            // Border
            var border = MakeImage(canvasGO.transform, "Border", BG_BORDER);
            CenterRect(border, new Vector2(692f, 432f));

            // Panel
            var panel = MakeImage(canvasGO.transform, "Panel", BG_PANEL);
            CenterRect(panel, new Vector2(680f, 420f));

            // Bottom input bar (anchored at bottom of panel)
            var inputBar = MakeImage(panel.transform, "InputBar", BG_INPUT);
            var ibRT = inputBar.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0f, 0f);
            ibRT.anchorMax = new Vector2(1f, 0f);
            ibRT.pivot     = new Vector2(0.5f, 0f);
            ibRT.anchoredPosition = Vector2.zero;
            ibRT.sizeDelta = new Vector2(0f, 36f);

            var sep = MakeImage(inputBar.transform, "Separator", BG_BORDER);
            var sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin = new Vector2(0f, 1f);
            sepRT.anchorMax = new Vector2(1f, 1f);
            sepRT.pivot     = new Vector2(0.5f, 1f);
            sepRT.anchoredPosition = Vector2.zero;
            sepRT.sizeDelta = new Vector2(0f, 1f);

            // ScrollRect fills panel above the input bar
            var scrollGO = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(panel.transform, false);
            scrollGO.GetComponent<Image>().color = Color.clear;  // transparent bg
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(0f, 36f);   // leave room for input bar
            scrollRT.offsetMax = new Vector2(0f, 0f);
            _scrollRect = scrollGO.GetComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical   = true;
            _scrollRect.scrollSensitivity = 30f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Mask / Viewport
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            viewportGO.GetComponent<Image>().color = Color.white; // alpha=1 required for stencil write
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;
            var viewRT = viewportGO.GetComponent<RectTransform>();
            StretchFull(viewRT);
            _scrollRect.viewport = viewRT;

            // Content: sized by ContentSizeFitter to fit all text
            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _contentRT = contentGO.GetComponent<RectTransform>();
            _contentRT.anchorMin = new Vector2(0f, 1f);
            _contentRT.anchorMax = new Vector2(1f, 1f);
            _contentRT.pivot     = new Vector2(0.5f, 1f);
            _contentRT.anchoredPosition = Vector2.zero;
            _contentRT.sizeDelta = Vector2.zero;
            var csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 8);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = true;
            _scrollRect.content = _contentRT;

            // TMP text inside content
            var textGO = new GameObject("TerminalText",
                typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGO.transform.SetParent(contentGO.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;
            _mainText = textGO.GetComponent<TextMeshProUGUI>();
            _mainText.fontSize = 13f;
            _mainText.color = C_GREEN;
            _mainText.alignment = TextAlignmentOptions.TopLeft;
            _mainText.overflowMode = TextOverflowModes.Overflow;
            _mainText.enableWordWrapping = false;
            _mainText.richText = true;
            var le = textGO.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        // ── UI helpers ───────────────────────────────────────────────────── //

        static RectTransform MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void CenterRect(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        // ── Ensure singleton activator ───────────────────────────────────── //

        static void EnsureActivator()
        {
            if (Fields.Core.CheatCodeActivator.Instance != null) return;
            var go = new GameObject("CheatCodeActivator");
            go.AddComponent<Fields.Core.CheatCodeActivator>();
        }
    }
}

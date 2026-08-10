using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fields.World
{
    /// <summary>
    /// Hozzáadandó a vintage_computer_3d_model objektumhoz.
    /// A játékos ránézve [E]-t nyom → megjelenik a retró DOS-stílusú terminál.
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

        // ── ASCII header ─────────────────────────────────────────────────── //
        const string HEADER =
            "+--------------------------------------------------+\n" +
            "| STUBBLE OS v1.0   (C) 1987 BARNYARD SYSTEMS INC |\n" +
            "| ** CHEATING IS IMMORAL. DO IT ANYWAY. **         |\n" +
            "+--------------------------------------------------+\n" +
            "  TYPE YOUR CHEAT CODE AND PRESS [ENTER]\n" +
            "  [ESC] TO EXIT LIKE A COWARD\n" +
            "--------------------------------------------------";

        const string PROMPT = "C:\\FARM> ";

        // ── Funny escalating failure messages ────────────────────────────── //
        static readonly string[] FAIL_MSGS =
        {
            "BAD COMMAND OR FILE NAME: {0}",
            "STILL NOT A VALID CODE. HAVE YOU TRIED 'HESOYAM'?",
            "ARE YOU JUST MASHING KEYS? THIS IS SAD.",
            "PLEASE. HAVE SOME DIGNITY.",
            "OK. I GIVE UP. JUST GOOGLE THE CODES LIKE EVERYONE ELSE.",
        };

        // ── Static state ─────────────────────────────────────────────────── //

        /// <summary>UIManager checks this to skip opening the pause menu while the terminal is open.</summary>
        public static bool IsAnyOpen { get; private set; }

        static CheatCodeTerminal _activeTerminal;

        /// <summary>UIManager calls this instead of opening the pause menu when ESC is pressed.</summary>
        public static void CloseIfOpen() => _activeTerminal?.CloseTerminal();

        // ── Runtime state ────────────────────────────────────────────────── //
        bool _isOpen;
        string _currentInput = "";
        readonly List<(string text, bool ok)> _history = new();
        int _failCount;

        // Cursor blink
        float _blinkTimer;
        bool _cursorVisible = true;

        // Delay keyboard subscription by one frame so the opening E-press is not captured
        bool _pendingSubscribe;

        // UI refs
        Canvas _canvas;
        TextMeshProUGUI _mainText;

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
            UnsubscribeKeyboard(); // safe to call even if not subscribed
        }

        void Update()
        {
            // One-frame delayed subscription: avoids capturing the opening E-press
            if (_pendingSubscribe)
            {
                _pendingSubscribe = false;
                if (Keyboard.current != null)
                    Keyboard.current.onTextInput += OnTextInput;
            }

            if (!_isOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // ESC is handled by UIManager.Update() — it calls CloseIfOpen() so the
            // pause menu never opens simultaneously. Do NOT poll ESC here.

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
            _currentInput = "";
            _cursorVisible = true;
            _blinkTimer = 0f;

            var pc = Fields.Core.PlayerController.Instance;
            if (pc != null) pc.InputLocked = true;

            // Switch to bare hand — prevents tool swings while typing
            pc?.GetComponentInChildren<Fields.Tools.ToolHolder>(true)?.EquipBareHand();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Delay subscription by one frame: prevents the opening E-press
            // from appearing as 'E' in the first input line
            _pendingSubscribe = true;

            RefreshDisplay();
        }

        void CloseTerminal()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsAnyOpen = false;
            _activeTerminal = null;
            _canvas.gameObject.SetActive(false);
            _pendingSubscribe = false;

            UnsubscribeKeyboard();

            var pc = Fields.Core.PlayerController.Instance;
            if (pc != null) pc.InputLocked = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ── Keyboard input ───────────────────────────────────────────────── //

        void SubscribeKeyboard()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;
        }

        void UnsubscribeKeyboard()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;
        }

        void OnTextInput(char c)
        {
            if (!_isOpen) return;
            // Filter control chars (Enter/Backspace handled in Update)
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

            // Echo command
            _history.Add(($"{PROMPT}{raw.ToUpper()}", true));

            var activator = Fields.Core.CheatCodeActivator.Instance;
            if (activator == null)
            {
                _history.Add(("[!!] CHEAT ENGINE OFFLINE. RESTART REQUIRED.", false));
            }
            else
            {
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

            _history.Add(("", true));

            // Keep history bounded
            while (_history.Count > 30) _history.RemoveAt(0);

            RefreshDisplay();
        }

        string BuildFailMsg(string raw)
        {
            int idx = Mathf.Min(_failCount, FAIL_MSGS.Length - 1);
            return string.Format(FAIL_MSGS[idx], raw.ToUpper());
        }

        // ── Display ──────────────────────────────────────────────────────── //

        void RefreshDisplay()
        {
            if (_mainText == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(HEADER);
            sb.AppendLine();

            foreach (var (text, ok) in _history)
            {
                if (string.IsNullOrEmpty(text)) { sb.AppendLine(); continue; }

                // Color tags: ok lines green, error lines amber/red
                if (text.StartsWith(PROMPT))
                    sb.AppendLine($"<color=#{ColorHex(C_DIM)}>{text}</color>");
                else if (text.StartsWith("  [OK]"))
                    sb.AppendLine($"<color=#{ColorHex(C_AMBER)}>{text}</color>");
                else if (text.StartsWith("  [!!]"))
                    sb.AppendLine($"<color=#{ColorHex(C_RED)}>{text}</color>");
                else
                    sb.AppendLine(text);
            }

            // Current input line
            string cursor = _cursorVisible ? "_" : " ";
            sb.Append($"<color=#{ColorHex(C_GREEN)}>{PROMPT}{_currentInput.ToUpper()}{cursor}</color>");

            _mainText.text = sb.ToString();
        }

        static string ColorHex(Color c)
        {
            return $"{ToByte(c.r):X2}{ToByte(c.g):X2}{ToByte(c.b):X2}";
        }

        static int ToByte(float f) => Mathf.RoundToInt(Mathf.Clamp01(f) * 255f);

        // ── UI construction ──────────────────────────────────────────────── //

        void BuildUI()
        {
            // Root canvas — ScreenSpaceOverlay so it sits on top of everything
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

            // Border (slightly larger than panel, same center)
            var border = MakeImage(canvasGO.transform, "Border", BG_BORDER);
            CenterRect(border, new Vector2(692f, 432f));

            // Panel
            var panel = MakeImage(canvasGO.transform, "Panel", BG_PANEL);
            CenterRect(panel, new Vector2(680f, 420f));

            // Main text area (fills panel, leaves 36px at bottom for input row)
            var textGO = new GameObject("TerminalText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(panel.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.offsetMin = new Vector2(14f, 40f);
            textRT.offsetMax = new Vector2(-14f, -12f);
            _mainText = textGO.GetComponent<TextMeshProUGUI>();
            _mainText.fontSize = 13f;
            _mainText.color = C_GREEN;
            _mainText.alignment = TextAlignmentOptions.TopLeft;
            _mainText.overflowMode = TextOverflowModes.Truncate;
            _mainText.enableWordWrapping = false;
            _mainText.richText = true;

            // Bottom input bar background
            var inputBar = MakeImage(panel.transform, "InputBar", BG_INPUT);
            var ibRT = inputBar.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0f, 0f);
            ibRT.anchorMax = new Vector2(1f, 0f);
            ibRT.pivot     = new Vector2(0.5f, 0f);
            ibRT.anchoredPosition = Vector2.zero;
            ibRT.sizeDelta = new Vector2(0f, 36f);

            // Separator line at top of input bar
            var sep = MakeImage(inputBar.transform, "Separator", BG_BORDER);
            var sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin = new Vector2(0f, 1f);
            sepRT.anchorMax = new Vector2(1f, 1f);
            sepRT.pivot     = new Vector2(0.5f, 1f);
            sepRT.anchoredPosition = Vector2.zero;
            sepRT.sizeDelta = new Vector2(0f, 1f);
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

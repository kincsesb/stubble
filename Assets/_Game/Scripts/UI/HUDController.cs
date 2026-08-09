using System.Collections;
using Fields.Core;
using Fields.Economy;
using Fields.Grass;
using Fields.Tools;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Minimal HUD: stamina/fuel bar, animated money counter, field completion %, carried bale count.
    /// All bars fade out when full (not visible when idle).
    /// Money counter animates over 0.4s — never snaps.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }
        [Header("Bars")]
        public MMProgressBar staminaBar;
        public MMProgressBar fuelBar;

        [Header("Money")]
        public TextMeshProUGUI moneyText;
        [Tooltip("Duration of the animated money count-up")]
        public float moneyAnimDuration = 0.4f;

        [Header("Completion")]
        public TextMeshProUGUI completionText;
        [Tooltip("Progress bar for field completion. Auto-created if null.")]
        public MMProgressBar grassCutBar;
        [Tooltip("Label inside the grass cut bar. Auto-created.")]
        public TextMeshProUGUI grassCutBarLabel;

        [Header("Carry indicator")]
        public TextMeshProUGUI baleCountText;

        [Header("Baling progress")]
        [Tooltip("Assign an MMProgressBar. Created from code if null.")]
        public MMProgressBar balingBar;
        [Tooltip("Label inside the bar showing progress %. Auto-created.")]
        public TextMeshProUGUI balingBarLabel;

        [Header("Interaction prompt")]
        [Tooltip("TMP text for context hints. Auto-created if null.")]
        public TextMeshProUGUI promptText;

        [Header("Crosshair")]
        public Image crosshair;
        [Tooltip("Crosshair pulse scale when tool hits grass")]
        public float crosshairPulseScale = 1.4f;

        [Header("Runtime references (assigned in scene)")]
        public PlayerController player;
        public ToolHolder toolHolder;
        public GrassField activeGrassField;

        // Cached foreground images for per-frame color tinting
        Image _staminaFgImg;
        Image _fuelFgImg;
        Image _balingFgImg;
        Image _grassCutFgImg;

        // Money animation
        float _displayedMoney;
        float _targetMoney;
        float _moneyVelocity;

        // Money punch
        float _moneyPunchTimer;
        const float MONEY_PUNCH_DURATION = 0.45f;

        // Crosshair pulse
        float _crosshairPulseTimer;
        const float CROSSHAIR_PULSE_DURATION = 0.18f;

        // Completion milestones
        int _lastMilestone;
        float _milestoneFlashTimer;
        const float MILESTONE_FLASH_DURATION = 1.2f;
        static readonly int[] MILESTONES = { 25, 50, 75, 100 };


        // Baling flash (on bale complete)
        float _balingFlashTimer;
        const float BALING_FLASH_DURATION = 0.7f;

        // Sell feel
        Canvas _floatingCanvas;

        // Tool tooltip override
        float _toolTipTimer;
        string _toolTipText;
        const float TOOLTIP_FADE_START = 0.4f;   // fade-out starts this many seconds before end

        // Stamina low feel
        float _staminaLowTimer;
        const float STAMINA_LOW_PULSE = 0.6f;

        // Bale drop feel
        float _baleDropTimer;
        const float BALE_DROP_DURATION = 0.9f;
        string _baleDropMessage;

        // Prompt fade
        float _promptAlpha;
        string _lastHint;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildFloatingCanvas();
        }

        void Start()
        {
            if (CurrencyManager.Instance != null)
            {
                _targetMoney = _displayedMoney = CurrencyManager.Instance.Money;
                CurrencyManager.Instance.OnMoneyChanged += OnMoneyChanged;
            }
            EnsureBalingBar();
            EnsureGrassCutBar();
            EnsurePromptText();
            _staminaFgImg = staminaBar?.ForegroundBar?.GetComponent<Image>();
            _fuelFgImg    = fuelBar?.ForegroundBar?.GetComponent<Image>();
        }

        void BuildFloatingCanvas()
        {
            var go = new GameObject("FloatingTextCanvas");
            go.transform.SetParent(transform.root, false);
            _floatingCanvas = go.AddComponent<Canvas>();
            _floatingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _floatingCanvas.sortingOrder = 998;
            go.AddComponent<CanvasScaler>();
        }

        void EnsureBalingBar()
        {
            if (balingBar != null) return;

            // Container: background Image + MMProgressBar
            var bgGO = new GameObject("BalingBarContainer", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0f);
            bgRT.anchorMax = new Vector2(0.5f, 0f);
            bgRT.anchoredPosition = new Vector2(0f, 78f);
            bgRT.sizeDelta = new Vector2(360f, 20f);
            bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            bgGO.SetActive(false);

            // ForegroundBar fill (child, full size, left-pivot so LocalScale fills left-to-right)
            var fgGO = new GameObject("BalingBarFill", typeof(RectTransform), typeof(Image));
            fgGO.transform.SetParent(bgGO.transform, false);
            var fgRT = fgGO.GetComponent<RectTransform>();
            fgRT.pivot      = new Vector2(0f, 0.5f);
            fgRT.anchorMin  = Vector2.zero;
            fgRT.anchorMax  = Vector2.one;
            fgRT.offsetMin  = new Vector2(2f, 2f);
            fgRT.offsetMax  = new Vector2(-2f, -2f);
            _balingFgImg = fgGO.GetComponent<Image>();
            _balingFgImg.color = new Color(0.9f, 0.7f, 0.1f);

            // MMProgressBar on container
            balingBar = bgGO.AddComponent<MMProgressBar>();
            balingBar.ForegroundBar = fgGO.transform;
            balingBar.FillMode = MMProgressBar.FillModes.LocalScale;
            balingBar.BarDirection = MMProgressBar.BarDirections.LeftToRight;
            balingBar.TimeScale = MMProgressBar.TimeScales.UnscaledTime;
            balingBar.LerpForegroundBar = true;
            balingBar.LerpForegroundBarSpeedIncreasing = 20f;
            balingBar.LerpForegroundBarSpeedDecreasing = 20f;

            // Label centered over the bar
            var labelGO = new GameObject("BalingBarLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(bgGO.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            balingBarLabel = labelGO.GetComponent<TextMeshProUGUI>();
            balingBarLabel.fontSize = 13;
            balingBarLabel.fontStyle = TMPro.FontStyles.Bold;
            balingBarLabel.alignment = TextAlignmentOptions.Center;
            balingBarLabel.color = new Color(1f, 1f, 1f, 0.9f);
            balingBarLabel.text = "";
        }

        void EnsureGrassCutBar()
        {
            if (grassCutBar != null)
            {
                _grassCutFgImg = grassCutBar.ForegroundBar?.GetComponent<Image>();
                if (completionText != null) completionText.gameObject.SetActive(false);
                return;
            }

            var bgGO = new GameObject("GrassCutBarContainer", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 1f);
            bgRT.anchorMax = new Vector2(0.5f, 1f);
            bgRT.anchoredPosition = new Vector2(0f, -36f);
            bgRT.sizeDelta = new Vector2(340f, 22f);
            bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var fgGO = new GameObject("GrassCutFill", typeof(RectTransform), typeof(Image));
            fgGO.transform.SetParent(bgGO.transform, false);
            var fgRT = fgGO.GetComponent<RectTransform>();
            fgRT.pivot     = new Vector2(0f, 0.5f);
            fgRT.anchorMin = Vector2.zero;
            fgRT.anchorMax = Vector2.one;
            fgRT.offsetMin = new Vector2(2f, 2f);
            fgRT.offsetMax = new Vector2(-2f, -2f);
            _grassCutFgImg = fgGO.GetComponent<Image>();
            _grassCutFgImg.color = new Color(0.25f, 0.8f, 0.3f, 0.85f);

            grassCutBar = bgGO.AddComponent<MMProgressBar>();
            grassCutBar.ForegroundBar = fgGO.transform;
            grassCutBar.FillMode = MMProgressBar.FillModes.LocalScale;
            grassCutBar.BarDirection = MMProgressBar.BarDirections.LeftToRight;
            grassCutBar.TimeScale = MMProgressBar.TimeScales.UnscaledTime;
            grassCutBar.LerpForegroundBar = true;
            grassCutBar.LerpForegroundBarSpeedIncreasing = 4f;
            grassCutBar.LerpForegroundBarSpeedDecreasing = 4f;

            // Label inside the bar
            var lGO = new GameObject("GrassCutLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lGO.transform.SetParent(bgGO.transform, false);
            var lRT = lGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero;
            lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero;
            lRT.offsetMax = Vector2.zero;
            grassCutBarLabel = lGO.GetComponent<TextMeshProUGUI>();
            grassCutBarLabel.fontSize = 12;
            grassCutBarLabel.fontStyle = TMPro.FontStyles.Bold;
            grassCutBarLabel.alignment = TextAlignmentOptions.Center;
            grassCutBarLabel.color = new Color(1f, 1f, 1f, 0.9f);
            grassCutBarLabel.text = "";

            grassCutBar.SetBar01(0f);
            // completionText replaced by the bar — always hide it
            if (completionText != null) completionText.gameObject.SetActive(false);
        }

        void EnsurePromptText()
        {
            if (promptText != null) return;
            var go = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 108f);
            rt.sizeDelta = new Vector2(600f, 36f);
            promptText = go.GetComponent<TextMeshProUGUI>();
            promptText.fontSize = 20;
            promptText.fontStyle = TMPro.FontStyles.Bold;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;
            go.SetActive(false);
        }

        void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
        }

        void Update()
        {
            UpdateBars();
            AnimateMoney();
            UpdateCompletion();
            UpdateBaleCount();
            UpdatePrompt();
            TickMoneyPunch();
            TickCrosshairPulse();
            TickBalingFlash();
            TickStaminaLow();
            TickBaleDropFeel();
        }

        /// <summary>Called by tools when blade connects with grass.</summary>
        public void PulseHit()
        {
            _crosshairPulseTimer = CROSSHAIR_PULSE_DURATION;
        }

        // ------------------------------------------------------------------ //

        bool IsGameActive => player != null && player.gameObject.activeInHierarchy;

        void UpdateBars()
        {
            if (!IsGameActive)
            {
                if (staminaBar != null) staminaBar.gameObject.SetActive(false);
                if (balingBar  != null) balingBar.gameObject.SetActive(false);
                if (fuelBar    != null) fuelBar.gameObject.SetActive(false);
                return;
            }
            if (player == null) return;

            // Stamina: melee tool pool when equipped, else player sprint pool
            float stamina = toolHolder?.ActiveTool is MeleeToolBase m
                ? m.StaminaNormalized
                : player.StaminaNormalized;
            if (staminaBar != null)
            {
                staminaBar.gameObject.SetActive(true);
                staminaBar.SetBar01(stamina);
                Color staminaColor = stamina > 0.6f
                    ? Color.Lerp(new Color(1f, 0.75f, 0f), new Color(0.15f, 0.85f, 0.25f), (stamina - 0.6f) / 0.4f)
                    : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.75f, 0f), stamina / 0.6f);
                if (stamina < 0.25f)
                {
                    float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                    staminaColor = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.5f, 0.1f), pulse);
                    float scl = 1f + pulse * 0.12f;
                    staminaBar.transform.localScale = new Vector3(scl, scl, 1f);
                    if (stamina < 0.15f && _staminaLowTimer <= 0f)
                    {
                        _staminaLowTimer = STAMINA_LOW_PULSE;
                        Fields.Feel.GameFeelController.Instance?.StartShake(0.12f, 0.012f, 20f);
                    }
                }
                else
                {
                    staminaBar.transform.localScale = Vector3.one;
                }
                if (_staminaFgImg != null) _staminaFgImg.color = staminaColor;
            }

            // Baling progress bar
            if (balingBar != null)
            {
                bool show = player.BalingReady || player.IsBaling;
                balingBar.gameObject.SetActive(show);

                if (show)
                {
                    float prog = player.BalingProgress;
                    balingBar.SetBar01(prog);

                    if (player.IsBaling)
                    {
                        // Pulse brightness while filling
                        float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.1f;
                        Color balingColor = Color.Lerp(
                            new Color(0.95f, 0.70f, 0.05f),
                            new Color(0.20f, 0.95f, 0.25f),
                            prog) + new Color(pulse, pulse, pulse, 0f);
                        if (_balingFgImg != null) _balingFgImg.color = balingColor;

                        // Scale bar slightly while active (feel)
                        float scl = 1f + Mathf.Sin(Time.time * 6f) * 0.015f;
                        balingBar.transform.localScale = new Vector3(scl, 1f + scl * 0.03f, 1f);

                        // Progress text inside the bar
                        if (balingBarLabel != null)
                            balingBarLabel.text = LocalizationManager.Instance != null
                                ? LocalizationManager.Instance.Get("hud.baling_progress", Mathf.RoundToInt(prog * 100))
                                : $"Baling...  {Mathf.RoundToInt(prog * 100)}%";
                    }
                    else
                    {
                        // Ready but not baling: dim gold idle pulse
                        float idle = (Mathf.Sin(Time.time * 1.5f) + 1f) * 0.15f;
                        if (_balingFgImg != null) _balingFgImg.color = new Color(0.9f, 0.75f, 0.1f, 0.4f + idle);
                        balingBar.SetBar01(0f);
                        balingBar.transform.localScale = Vector3.one;
                        if (balingBarLabel != null)
                            balingBarLabel.text = LocalizationManager.Instance != null
                                ? LocalizationManager.Instance.Get("hud.baling_ready")
                                : "Hold  [E]  to bale";
                    }
                }
                else
                {
                    balingBar.transform.localScale = Vector3.one;
                    if (balingBarLabel != null) balingBarLabel.text = "";
                }
            }

            // Fuel bar for powered tool
            float fuel = 0f;
            bool showFuel = false;
            if (toolHolder?.ActiveTool is PoweredToolBase poweredTool)
            {
                fuel = poweredTool.FuelNormalized;
                showFuel = true;
            }
            if (fuelBar != null)
            {
                fuelBar.gameObject.SetActive(showFuel);
                if (showFuel)
                {
                    fuelBar.SetBar01(fuel);
                    // Color: blue (full) → orange → red (empty)
                    Color fuelColor = fuel > 0.3f
                        ? Color.Lerp(new Color(1f, 0.55f, 0.05f), new Color(0.15f, 0.55f, 1f), (fuel - 0.3f) / 0.7f)
                        : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.05f), fuel / 0.3f);
                    if (_fuelFgImg != null) _fuelFgImg.color = fuelColor;
                }
            }
        }

        void AnimateMoney()
        {
            if (moneyText == null) return;
            _displayedMoney = Mathf.SmoothDamp(
                _displayedMoney, _targetMoney, ref _moneyVelocity,
                moneyAnimDuration, float.MaxValue, Time.deltaTime);

            int m = Mathf.RoundToInt(_displayedMoney);
            moneyText.text = Fields.Core.LocalizationManager.Instance != null
                ? Fields.Core.LocalizationManager.Instance.Get("hud.money", m)
                : $"$ {m}";
        }

        void UpdateCompletion()
        {
            if (!IsGameActive)
            {
                if (grassCutBar != null) grassCutBar.gameObject.SetActive(false);
                return;
            }
            if (activeGrassField == null) return;
            if (grassCutBar != null) grassCutBar.gameObject.SetActive(true);
            float pct = activeGrassField.GetCompletionPercent();

            // Grass cut progress bar
            if (grassCutBar != null)
            {
                grassCutBar.SetBar01(pct / 100f);
                if (_grassCutFgImg != null)
                    _grassCutFgImg.color = Color.Lerp(
                        new Color(0.6f, 0.85f, 0.3f, 0.85f),
                        new Color(0.15f, 0.9f, 0.25f, 0.85f),
                        pct / 100f);
            }

            // Label inside the bar (only source of the % display)
            if (grassCutBarLabel != null)
                grassCutBarLabel.text = Fields.Core.LocalizationManager.Instance != null
                    ? Fields.Core.LocalizationManager.Instance.Get("hud.completion", (int)pct)
                    : $"{pct:F0}%";

            // Milestone flash
            int milestone = 0;
            foreach (int m in MILESTONES) if (pct >= m) milestone = m;
            if (milestone > _lastMilestone)
            {
                _lastMilestone = milestone;
                _milestoneFlashTimer = MILESTONE_FLASH_DURATION;
            }

            if (_milestoneFlashTimer > 0f)
            {
                _milestoneFlashTimer -= Time.deltaTime;
                float t = _milestoneFlashTimer / MILESTONE_FLASH_DURATION;
                if (grassCutBarLabel != null)
                {
                    grassCutBarLabel.color = Color.Lerp(Color.white, new Color(0.3f, 1f, 0.3f), t);
                    grassCutBarLabel.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, t);
                }
            }
            else
            {
                if (grassCutBarLabel != null)
                {
                    grassCutBarLabel.color = new Color(1f, 1f, 1f, 0.9f);
                    grassCutBarLabel.transform.localScale = Vector3.one;
                }
            }
        }

        void UpdateBaleCount()
        {
            if (baleCountText == null || player == null) return;
            int count = player.CarriedBaleCount;
            baleCountText.text = count > 0
                ? (Fields.Core.LocalizationManager.Instance != null
                    ? Fields.Core.LocalizationManager.Instance.Get("hud.bales", count)
                    : $"[{count}]")
                : string.Empty;
            baleCountText.gameObject.SetActive(count > 0);
        }

        void TickMoneyPunch()
        {
            if (moneyText == null) return;
            if (_moneyPunchTimer > 0f)
            {
                _moneyPunchTimer -= Time.deltaTime;
                float t = _moneyPunchTimer / MONEY_PUNCH_DURATION;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
                moneyText.transform.localScale = Vector3.one * scale;
                // Flash green on earn: bright green → white settle
                moneyText.color = Color.Lerp(Color.white, new Color(0.25f, 1f, 0.45f), t);
            }
            else
            {
                moneyText.transform.localScale = Vector3.one;
                moneyText.color = Color.white;
            }
        }

        void TickStaminaLow()
        {
            if (_staminaLowTimer > 0f)
                _staminaLowTimer -= Time.deltaTime;
        }

        void TickBaleDropFeel()
        {
            if (_baleDropTimer <= 0f) return;
            _baleDropTimer -= Time.deltaTime;

            if (promptText == null) return;
            float t = _baleDropTimer / BALE_DROP_DURATION;
            promptText.gameObject.SetActive(true);
            promptText.text = _baleDropMessage;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.14f;
            promptText.transform.localScale = Vector3.one * scale;
            promptText.color = new Color(0.95f, 0.85f, 0.25f, Mathf.Clamp01(t * 3f));
        }

        // ------------------------------------------------------------------ //
        // Tool tooltip
        // ------------------------------------------------------------------ //

        public void ShowToolTip(string text, float duration)
        {
            _toolTipText  = text;
            _toolTipTimer = duration;
        }

        void TickCrosshairPulse()
        {
            if (crosshair == null) return;
            if (_crosshairPulseTimer > 0f)
            {
                _crosshairPulseTimer -= Time.deltaTime;
                float t = _crosshairPulseTimer / CROSSHAIR_PULSE_DURATION;
                float s = Mathf.Lerp(1f, crosshairPulseScale, t);
                crosshair.transform.localScale = Vector3.one * s;
            }
            else
            {
                crosshair.transform.localScale = Vector3.one;
            }
        }

        void OnMoneyChanged(int oldVal, int newVal)
        {
            _targetMoney = newVal;
            if (newVal > oldVal) _moneyPunchTimer = MONEY_PUNCH_DURATION;
        }

        // ------------------------------------------------------------------ //
        // Prompt text (context hints)
        // ------------------------------------------------------------------ //

        void UpdatePrompt()
        {
            if (promptText == null || player == null) return;

            // Highest priority: baling flash (bale just created)
            if (_balingFlashTimer > 0f)
            {
                promptText.gameObject.SetActive(true);
                float t = _balingFlashTimer / BALING_FLASH_DURATION;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                promptText.transform.localScale = Vector3.one * scale;
                promptText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("hud.bale_created")
                    : "✓  Bale Created!";
                promptText.color = Color.Lerp(new Color(0.2f, 1f, 0.3f, 0f), new Color(0.2f, 1f, 0.3f, 1f), t);
                return;
            }

            // Bale drop feel (handled by TickBaleDropFeel — it owns the prompt)
            if (_baleDropTimer > 0f) return;

            // Tool tooltip override (shown after equip, fades out)
            if (_toolTipTimer > 0f)
            {
                _toolTipTimer -= Time.deltaTime;
                float alpha = _toolTipTimer < TOOLTIP_FADE_START
                    ? _toolTipTimer / TOOLTIP_FADE_START
                    : 1f;
                promptText.gameObject.SetActive(true);
                promptText.text = _toolTipText;
                promptText.transform.localScale = Vector3.one;
                promptText.color = new Color(0.85f, 0.95f, 1f, alpha * 0.9f);
                return;
            }

            promptText.transform.localScale = Vector3.one;

            string hint = player.GetInteractHint();
            bool show = !string.IsNullOrEmpty(hint);
            promptText.gameObject.SetActive(show);
            if (!show) return;

            // Smooth alpha transition when hint changes
            if (hint != _lastHint) { _promptAlpha = 0f; _lastHint = hint; }
            _promptAlpha = Mathf.MoveTowards(_promptAlpha, 1f, Time.deltaTime * 6f);

            promptText.text = hint;

            if (player.IsBaling)
            {
                // Green gradient as progress fills
                promptText.color = new Color(
                    Mathf.Lerp(1f, 0.2f, player.BalingProgress),
                    Mathf.Lerp(0.85f, 1f, player.BalingProgress),
                    Mathf.Lerp(0.1f, 0.3f, player.BalingProgress),
                    _promptAlpha);
            }
            else if (player.BalingReady)
            {
                // Gold pulsing hint
                float p = (Mathf.Sin(Time.time * 2.5f) + 1f) * 0.35f + 0.3f;
                promptText.color = new Color(1f, 0.88f, 0.2f, _promptAlpha * p);
            }
            else
            {
                promptText.color = new Color(1f, 1f, 1f, _promptAlpha * 0.88f);
            }
        }

        // ------------------------------------------------------------------ //
        // Baling flash (called by PlayerController.CompleteBaling)
        // ------------------------------------------------------------------ //

        public void TriggerBalingFlash()
        {
            _balingFlashTimer = BALING_FLASH_DURATION;
        }

        void TickBalingFlash()
        {
            if (_balingFlashTimer > 0f)
                _balingFlashTimer -= Time.deltaTime;
        }

        // ------------------------------------------------------------------ //
        // Sell feel — floating money popup
        // ------------------------------------------------------------------ //

        public void TriggerBaleDropFeel(int baleCount)
        {
            _baleDropTimer   = BALE_DROP_DURATION;
            _baleDropMessage = LocalizationManager.Instance != null
                ? (baleCount == 1
                    ? LocalizationManager.Instance.Get("hud.bale_drop_single")
                    : LocalizationManager.Instance.Get("hud.bale_drop_multi", baleCount))
                : (baleCount == 1 ? "Bale dropped" : $"{baleCount} bales dropped");
        }

        public void TriggerSellFeel(int earned)
        {
            _moneyPunchTimer = MONEY_PUNCH_DURATION;
            if (_floatingCanvas != null && earned > 0)
                StartCoroutine(FloatingMoneyRoutine(earned));
        }

        IEnumerator FloatingMoneyRoutine(int earned)
        {
            var go = new GameObject("FloatMoney");
            go.transform.SetParent(_floatingCanvas.transform, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor near top-right where the money counter lives
            rt.anchorMin = rt.anchorMax = new Vector2(0.88f, 0.90f);
            rt.anchoredPosition = new Vector2(
                Random.Range(-30f, 30f),
                Random.Range(-10f, 20f));
            rt.sizeDelta = new Vector2(300f, 80f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 52;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = $"+${earned}";

            const float riseSpeed = 70f;
            const float life = 1.3f;
            float t = 0f;

            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float n = t / life;
                // Flash white on spawn then shift to green
                Color baseColor = n < 0.1f
                    ? Color.Lerp(Color.white, new Color(0.3f, 1f, 0.45f), n / 0.1f)
                    : new Color(0.3f, 1f, 0.45f);
                float alpha = n < 0.15f
                    ? n / 0.15f
                    : 1f - Mathf.Pow((n - 0.15f) / 0.85f, 1.5f);
                float scale = n < 0.12f
                    ? Mathf.Lerp(1.6f, 1f, n / 0.12f)
                    : 1f;

                tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                go.transform.localScale = Vector3.one * scale;
                rt.anchoredPosition += new Vector2(0f, riseSpeed * Time.unscaledDeltaTime);
                yield return null;
            }

            Destroy(go);
        }
    }
}
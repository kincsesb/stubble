using Fields.Core;
using Fields.Economy;
using Fields.Grass;
using Fields.Tools;
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
        public Image staminaBar;
        public Image fuelBar;

        [Header("Money")]
        public TextMeshProUGUI moneyText;
        [Tooltip("Duration of the animated money count-up")]
        public float moneyAnimDuration = 0.4f;

        [Header("Completion")]
        public TextMeshProUGUI completionText;

        [Header("Carry indicator")]
        public TextMeshProUGUI baleCountText;

        [Header("Baling progress")]
        [Tooltip("Assign a bar Image (fillMethod=Horizontal). Created from code if null.")]
        public Image balingBar;
        [Tooltip("Optional background behind the baling bar.")]
        public Image balingBarBg;
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

        // Money animation
        float _displayedMoney;
        float _targetMoney;
        float _moneyVelocity;

        // Money punch
        float _moneyPunchTimer;
        const float MONEY_PUNCH_DURATION = 0.25f;

        // Crosshair pulse
        float _crosshairPulseTimer;
        const float CROSSHAIR_PULSE_DURATION = 0.18f;

        // Completion milestones
        int _lastMilestone;
        float _milestoneFlashTimer;
        const float MILESTONE_FLASH_DURATION = 1.2f;
        static readonly int[] MILESTONES = { 25, 50, 75, 100 };

        // Bar fade (fuel only)
        float _fuelFadeTimer;
        const float FADE_DELAY = 1.5f;
        const float FADE_DURATION = 0.4f;

        // Baling flash (on bale complete)
        float _balingFlashTimer;
        const float BALING_FLASH_DURATION = 0.7f;

        // Prompt fade
        float _promptAlpha;
        string _lastHint;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (CurrencyManager.Instance != null)
            {
                _targetMoney = _displayedMoney = CurrencyManager.Instance.Money;
                CurrencyManager.Instance.OnMoneyChanged += OnMoneyChanged;
            }
            EnsureBalingBar();
            EnsurePromptText();
        }

        void EnsureBalingBar()
        {
            if (balingBar != null) return;

            // Dark background track
            var bgGO = new GameObject("BalingBarBg", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0f);
            bgRT.anchorMax = new Vector2(0.5f, 0f);
            bgRT.anchoredPosition = new Vector2(0f, 78f);
            bgRT.sizeDelta = new Vector2(360f, 20f);
            balingBarBg = bgGO.GetComponent<Image>();
            balingBarBg.color = new Color(0f, 0f, 0f, 0.55f);
            bgGO.SetActive(false);

            // Fill bar (child of bg)
            var go = new GameObject("BalingBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(bgGO.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2f, 2f);
            rt.offsetMax = new Vector2(-2f, -2f);
            balingBar = go.GetComponent<Image>();
            balingBar.color = new Color(0.9f, 0.7f, 0.1f);
            balingBar.type = Image.Type.Filled;
            balingBar.fillMethod = Image.FillMethod.Horizontal;
            balingBar.fillAmount = 0f;

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
        }

        /// <summary>Called by tools when blade connects with grass.</summary>
        public void PulseHit()
        {
            _crosshairPulseTimer = CROSSHAIR_PULSE_DURATION;
        }

        // ------------------------------------------------------------------ //

        void UpdateBars()
        {
            if (player == null) return;

            // Stamina: melee tool pool when equipped, else player sprint pool
            float stamina = toolHolder?.ActiveTool is MeleeToolBase m
                ? m.StaminaNormalized
                : player.StaminaNormalized;
            if (staminaBar != null)
            {
                bool showStamina = stamina < 0.999f;
                staminaBar.gameObject.SetActive(showStamina);
                if (showStamina)
                {
                    staminaBar.fillAmount = stamina;
                    Color staminaColor = stamina > 0.6f
                        ? Color.Lerp(new Color(1f, 0.75f, 0f), new Color(0.15f, 0.85f, 0.25f), (stamina - 0.6f) / 0.4f)
                        : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.75f, 0f), stamina / 0.6f);
                    staminaBar.color = staminaColor;
                }
            }

            // Baling progress bar
            if (balingBar != null)
            {
                bool show = player.BalingReady || player.IsBaling;
                // Show bg + bar together
                if (balingBarBg != null) balingBarBg.gameObject.SetActive(show);
                else balingBar.gameObject.SetActive(show);

                if (show)
                {
                    float prog = player.BalingProgress;
                    balingBar.fillAmount = prog;

                    if (player.IsBaling)
                    {
                        // Pulse brightness while filling
                        float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.1f;
                        balingBar.color = Color.Lerp(
                            new Color(0.95f, 0.70f, 0.05f),
                            new Color(0.20f, 0.95f, 0.25f),
                            prog) + new Color(pulse, pulse, pulse, 0f);

                        // Scale bar slightly while active (feel)
                        float scl = 1f + Mathf.Sin(Time.time * 6f) * 0.015f;
                        if (balingBarBg != null)
                            balingBarBg.transform.localScale = new Vector3(scl, 1f + scl * 0.03f, 1f);

                        // Progress text inside the bar
                        if (balingBarLabel != null)
                            balingBarLabel.text = $"Baling...  {Mathf.RoundToInt(prog * 100)}%";
                    }
                    else
                    {
                        // Ready but not baling: dim gold idle pulse
                        float idle = (Mathf.Sin(Time.time * 1.5f) + 1f) * 0.15f;
                        balingBar.color = new Color(0.9f, 0.75f, 0.1f, 0.4f + idle);
                        balingBar.fillAmount = 0f;
                        if (balingBarBg != null) balingBarBg.transform.localScale = Vector3.one;
                        if (balingBarLabel != null) balingBarLabel.text = "Hold  [E]  to bale";
                    }
                }
                else
                {
                    if (balingBarBg != null) balingBarBg.transform.localScale = Vector3.one;
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
                    fuelBar.fillAmount = fuel;
                    // Color: blue (full) → orange → red (empty)
                    Color fuelColor = fuel > 0.3f
                        ? Color.Lerp(new Color(1f, 0.55f, 0.05f), new Color(0.15f, 0.55f, 1f), (fuel - 0.3f) / 0.7f)
                        : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.05f), fuel / 0.3f);
                    {
                        _fuelFadeTimer = 0f;
                        fuelBar.color = fuelColor;
                    }
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
            if (completionText == null || activeGrassField == null) return;
            float pct = activeGrassField.GetCompletionPercent();
            completionText.text = Fields.Core.LocalizationManager.Instance != null
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
                completionText.color = Color.Lerp(Color.white, new Color(0.3f, 1f, 0.3f), t);
                completionText.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, t);
            }
            else
            {
                completionText.color = Color.white;
                completionText.transform.localScale = Vector3.one;
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
            if (moneyText == null || _moneyPunchTimer <= 0f) return;
            _moneyPunchTimer -= Time.deltaTime;
            float t = _moneyPunchTimer / MONEY_PUNCH_DURATION;
            // Bounce: up then back
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
            moneyText.transform.localScale = Vector3.one * scale;
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

            // Flash override: bale just completed
            if (_balingFlashTimer > 0f)
            {
                promptText.gameObject.SetActive(true);
                float t = _balingFlashTimer / BALING_FLASH_DURATION;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                promptText.transform.localScale = Vector3.one * scale;
                promptText.text = "✓  Bale Created!";
                promptText.color = Color.Lerp(new Color(0.2f, 1f, 0.3f, 0f), new Color(0.2f, 1f, 0.3f, 1f), t);
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
    }
}
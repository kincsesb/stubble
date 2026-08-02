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
        }

        void EnsureBalingBar()
        {
            if (balingBar != null) return;
            // Build a simple progress bar above the stamina bar if none is assigned
            var go = new GameObject("BalingBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 78f);
            rt.sizeDelta = new Vector2(350f, 16f);
            balingBar = go.GetComponent<Image>();
            balingBar.color = new Color(0.9f, 0.7f, 0.1f);
            balingBar.type = Image.Type.Filled;
            balingBar.fillMethod = Image.FillMethod.Horizontal;
            balingBar.fillAmount = 0f;
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
            TickMoneyPunch();
            TickCrosshairPulse();
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

            float stamina = player.StaminaNormalized;
            if (staminaBar != null)
            {
                staminaBar.fillAmount = stamina;
                // Always visible; color: green → yellow → red
                Color staminaColor = stamina > 0.6f
                    ? Color.Lerp(new Color(1f, 0.75f, 0f), new Color(0.15f, 0.85f, 0.25f), (stamina - 0.6f) / 0.4f)
                    : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.75f, 0f), stamina / 0.6f);
                staminaBar.color = staminaColor;
            }

            // Baling progress bar: shows when hay is nearby OR actively baling
            if (balingBar != null)
            {
                bool show = player.BalingReady || player.IsBaling;
                balingBar.gameObject.SetActive(show);
                if (show)
                {
                    balingBar.fillAmount = player.BalingProgress;
                    // Yellow-green while ready-idle, bright green while actively holding E
                    balingBar.color = player.IsBaling
                        ? Color.Lerp(new Color(0.9f, 0.7f, 0.1f), new Color(0.2f, 0.95f, 0.2f), player.BalingProgress)
                        : new Color(0.9f, 0.75f, 0.1f, 0.6f); // dimmer when just ready
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
                    // Color: blue → orange → red + fade when full
                    Color fuelColor = fuel > 0.3f
                        ? Color.Lerp(new Color(1f, 0.55f, 0.05f), new Color(0.15f, 0.55f, 1f), (fuel - 0.3f) / 0.7f)
                        : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.05f), fuel / 0.3f);
                    if (fuel >= 0.999f)
                    {
                        _fuelFadeTimer += Time.deltaTime;
                        float t = Mathf.Clamp01((_fuelFadeTimer - FADE_DELAY) / FADE_DURATION);
                        fuelBar.color = new Color(fuelColor.r, fuelColor.g, fuelColor.b, 1f - t);
                    }
                    else
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
    }
}
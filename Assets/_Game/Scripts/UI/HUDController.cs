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

        // Bar fade
        float _staminaFadeTimer;
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

                // Fade when full
                if (stamina >= 0.999f)
                {
                    _staminaFadeTimer += Time.deltaTime;
                    float t = Mathf.Clamp01((_staminaFadeTimer - FADE_DELAY) / FADE_DURATION);
                    var c = staminaBar.color;
                    staminaBar.color = new Color(c.r, c.g, c.b, 1f - t);
                }
                else
                {
                    _staminaFadeTimer = 0f;
                    var c = staminaBar.color;
                    staminaBar.color = new Color(c.r, c.g, c.b, 1f);
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
                    if (fuel >= 0.999f)
                    {
                        _fuelFadeTimer += Time.deltaTime;
                        float t = Mathf.Clamp01((_fuelFadeTimer - FADE_DELAY) / FADE_DURATION);
                        var c = fuelBar.color;
                        fuelBar.color = new Color(c.r, c.g, c.b, 1f - t);
                    }
                    else
                    {
                        _fuelFadeTimer = 0f;
                        var c = fuelBar.color;
                        fuelBar.color = new Color(c.r, c.g, c.b, 1f);
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

            moneyText.text = $"$ {Mathf.RoundToInt(_displayedMoney)}";
        }

        void UpdateCompletion()
        {
            if (completionText == null || activeGrassField == null) return;
            float pct = activeGrassField.GetCompletionPercent();
            completionText.text = $"{pct:F0}%";

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
            baleCountText.text = count > 0 ? $"[{count}]" : string.Empty;
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
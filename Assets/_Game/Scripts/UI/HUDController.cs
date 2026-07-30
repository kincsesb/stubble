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

        [Header("Runtime references (assigned in scene)")]
        public PlayerController player;
        public ToolHolder toolHolder;
        public GrassField activeGrassField;

        // Money animation
        float _displayedMoney;
        float _targetMoney;
        float _moneyVelocity;

        // Bar fade
        float _staminaFadeTimer;
        float _fuelFadeTimer;
        const float FADE_DELAY = 1.5f;
        const float FADE_DURATION = 0.4f;

        // ------------------------------------------------------------------ //

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
            completionText.text = $"{activeGrassField.GetCompletionPercent():F0}%";
        }

        void UpdateBaleCount()
        {
            if (baleCountText == null || player == null) return;
            int count = player.CarriedBaleCount;
            baleCountText.text = count > 0 ? $"[{count}]" : string.Empty;
            baleCountText.gameObject.SetActive(count > 0);
        }

        void OnMoneyChanged(int oldVal, int newVal) => _targetMoney = newVal;
    }
}
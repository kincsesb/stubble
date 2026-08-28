using TMPro;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Put this on the Throwed_Bale_Billboard scene object.
    /// Wire the TextMeshPro component in the Inspector.
    /// Tracks the all-time throw distance record and updates the text when beaten.
    /// </summary>
    public class ThrowedBaleBillboard : MonoBehaviour
    {
        [Tooltip("TMP component that displays the throw record")]
        public TextMeshPro text;
        [Tooltip("Optional shadow/depth layer TMP (same text, dark color, slightly behind)")]
        public TextMeshPro shadowText;

        [Tooltip("Color when record is beaten (bright pulse A)")]
        public Color colorA = new Color(1f, 0.95f, 0.2f, 1f);
        [Tooltip("Color pulse B (white flash)")]
        public Color colorB = Color.white;
        [Tooltip("Pulse cycles per second")]
        public float pulseSpeed = 1.8f;

        float _recordDistance = -1f;
        float _flashTimer;
        const float FlashDuration = 3f;

        void Start()
        {
            if (text != null) text.gameObject.SetActive(false);
            if (shadowText != null) shadowText.gameObject.SetActive(false);
        }

        void Update()
        {
            if (text == null || _recordDistance < 0f) return;

            Color col;
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                col = Color.Lerp(colorA, colorB, t);
            }
            else
            {
                col = colorA;
            }
            text.color = col;
        }

        /// <summary>
        /// Called by DeliveryZone on every square bale delivery.
        /// Updates the billboard only if distance beats the current record.
        /// </summary>
        public void ReportDelivery(string playerName, float distance)
        {
            if (distance <= _recordDistance) return;

            _recordDistance = distance;
            _flashTimer = FlashDuration;

            int meters = Mathf.RoundToInt(distance);
            string line = meters == 0 ? $"{playerName}\n0m" : $"{playerName}\n{meters}m";

            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.text = line;
            }
            if (shadowText != null)
            {
                shadowText.gameObject.SetActive(true);
                shadowText.text = line;
            }
        }
    }
}
using Fields.Core;
using Fields.Economy;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Trigger zone: when the player enters carrying SquareBales, they are auto-sold for money.
    /// Place a trigger Collider on this GameObject and set the layer to Default or a dedicated Trigger layer.
    /// </summary>
    public class DeliveryZone : MonoBehaviour
    {
        [Tooltip("Price per bale (overridden by BalerManager hay value if set)")]
        public int pricePerBale = 50;

        [Header("Visual feedback")]
        [Tooltip("Optional particle effect played on each sale")]
        public ParticleSystem saleEffect;

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            var bales = player.GetCarriedSquareBales();
            if (bales.Count == 0) return;

            int total = CalcTotal(bales.Count);
            CurrencyManager.Instance?.Earn(total);
            player.DropSquareBales();

            // Destroy the dropped bales immediately — they were just sold
            var dropped = Object.FindObjectsByType<SquareBale>(FindObjectsSortMode.None);
            foreach (var b in dropped)
            {
                float dist = Vector3.Distance(b.transform.position, transform.position);
                if (dist < 10f) Object.Destroy(b.gameObject);
            }

            saleEffect?.Play();
            Debug.Log($"[DeliveryZone] Sold {bales.Count} bale(s) for ${total}");
        }

        int CalcTotal(int baleCount)
        {
            var bm = Fields.Economy.BalerManager.Instance;
            float mult = bm != null ? bm.HayValueMultiplier : 1f;
            return Mathf.RoundToInt(pricePerBale * baleCount * mult);
        }
    }
}
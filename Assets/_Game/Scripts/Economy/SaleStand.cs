using Fields.Core;
using Fields.Hay;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Stand trigger — sells carried bales and opens the shop.
    /// Player walks into trigger zone; Interact key opens shop or sells bales.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SaleStand : MonoBehaviour, IInteractable
    {
        [Header("References")]
        public ShopPlaceholder shop;

        [Header("Economy")]
        [Tooltip("Base value per hay unit in a bale")]
        public float hayUnitValue = 2f;

        public void Interact(PlayerController player)
        {
            // Sell carried bales first, then open shop
            SellBales(player);
            shop?.ToggleOpen();
        }

        void SellBales(PlayerController player)
        {
            if (player.CarriedBaleCount == 0) return;
            // In P0, selling is simplified — real implementation in P1-05
            // (animated money counter, multi-bale pitch-chain audio)
            int totalEarned = player.CarriedBaleCount * 60; // placeholder valuation
            CurrencyManager.Instance?.Earn(totalEarned);
            // TODO P1: DropAllBales(player) + animate
        }
    }
}
using Fields.Core;
using Fields.Hay;
using Fields.UI;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Stand trigger — sells carried bales and opens the shop.
    /// Player walks into trigger zone; Interact key sells bales then opens ShopUI.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SaleStand : MonoBehaviour, IInteractable
    {
        [Header("References")]
        public ShopUI shop;

        [Header("Economy")]
        [Tooltip("Base value per hay unit in a bale")]
        public float hayUnitValue = 2f;

        public void Interact(PlayerController player)
        {
            SellBales(player);
            shop?.Open();
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
using Fields.Core;
using Fields.Hay;
using Fields.Tools;
using Fields.UI;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Sale stand — sells all carried bales (HayPile + SquareBale) then opens shop.
    /// Value = hayUnits × hayUnitValue × GameConfig.hayValueMultiplier[parcelIndex].
    /// Bales are destroyed on sale (CurrencyManager.Earn triggers animated HUD counter).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SaleStand : MonoBehaviour, IInteractable
    {
        [Header("References")]
        public ShopUI shop;

        [Header("Economy")]
        [Tooltip("Base $ value per hay unit")]
        public float hayUnitValue = 2f;
        [Tooltip("Parcel quality multiplier (set per stand in Inspector)")]
        public float parcelMultiplier = 1f;

        bool _firstSaleDone;

        public void Interact(PlayerController player)
        {
            int earned = SellBales(player);
            if (earned > 0)
            {
                CurrencyManager.Instance?.Earn(earned);
                if (!_firstSaleDone)
                {
                    _firstSaleDone = true;
                    Fields.Core.SteamManager.Instance?.OnFirstSale();
                }
            }

            // Spec §5.5: powered tools refuel free at the stand.
            player.GetComponentInChildren<ToolHolder>()?.RefuelAllPowered();

            shop?.Open();
        }

        int SellBales(PlayerController player)
        {
            if (player.CarriedBaleCount == 0) return 0;

            float multiplier = GetParcelMultiplier();
            int total = 0;

            // Sell square bales
            total += SellSquareBales(player, multiplier);

            // Sell hay piles (legacy carry path)
            total += SellHayPiles(player, multiplier);

            return total;
        }

        int SellSquareBales(PlayerController player, float multiplier)
        {
            // Collect bales before dropping (DropSquareBales destroys carry refs)
            var bales = player.GetCarriedSquareBales();
            int earned = 0;
            foreach (var bale in bales)
                earned += Mathf.RoundToInt(bale.HayUnits * hayUnitValue * multiplier);

            player.DropSquareBales(); // physically drop then destroy
            foreach (var bale in bales)
                if (bale != null) Object.Destroy(bale.gameObject);

            return earned;
        }

        int SellHayPiles(PlayerController player, float multiplier)
        {
            int earned = 0;
            int count = player.CarriedBaleCount; // remaining hay piles
            for (int i = 0; i < count; i++)
                earned += Mathf.RoundToInt(60 * hayUnitValue * multiplier);

            player.DropAllHayPiles(destroy: true);
            return earned;
        }

        // Spec §6.4: apply hay value upgrade multiplier on top of parcel quality.
        float GetParcelMultiplier() =>
            parcelMultiplier * (BalerManager.Instance?.HayValueMultiplier ?? 1f);
    }
}

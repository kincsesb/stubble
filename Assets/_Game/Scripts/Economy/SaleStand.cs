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
            var bales = player.GetCarriedSquareBales();
            int earned = 0;
            foreach (var bale in bales)
            {
                int value = Mathf.RoundToInt(bale.HayUnits * hayUnitValue * multiplier);
                earned += value;
                // Attribute revenue to the parcel the hay was CUT in, not the stand's parcel.
                Fields.Core.GameEvents.FireBaleSold(bale.OriginParcelIndex, 0, value);
            }

            player.DropSquareBales();
            foreach (var bale in bales)
                if (bale != null) Object.Destroy(bale.gameObject);
            return earned;
        }

        // Spec §6.4: apply hay value upgrade multiplier on top of parcel quality.
        float GetParcelMultiplier() =>
            parcelMultiplier * (BalerManager.Instance?.HayValueMultiplier ?? 1f);
    }
}

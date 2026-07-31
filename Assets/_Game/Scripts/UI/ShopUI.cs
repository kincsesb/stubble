using System.Collections.Generic;
using Fields.Core.Data;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Minimal in-world shop panel. Three tabs: Tools | Upgrades | Unlocks.
    /// Toggled by SaleStand.Interact(). Pauses time while open.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("Panel root (set active to show/hide)")]
        public GameObject shopPanel;

        [Header("Tab buttons")]
        public Button tabTools;
        public Button tabUpgrades;
        public Button tabUnlocks;

        [Header("Content area")]
        public Transform contentParent;
        public GameObject rowPrefab; // simple row: [name label] [price label] [button]

        [Header("Close")]
        public Button closeButton;

        [Header("Data (assign in Inspector)")]
        public List<ToolData> allTools;
        public List<ParcelData> allParcels;

        ToolUnlockManager _toolMgr;
        ParcelManager _parcelMgr;
        CurrencyManager _currency;
        int _activeTab;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            tabTools.onClick.AddListener(() => ShowTab(0));
            tabUpgrades.onClick.AddListener(() => ShowTab(1));
            tabUnlocks.onClick.AddListener(() => ShowTab(2));
            closeButton.onClick.AddListener(Close);
            shopPanel.SetActive(false);
        }

        void Start()
        {
            _toolMgr   = ToolUnlockManager.Instance;
            _parcelMgr = ParcelManager.Instance;
            _currency  = CurrencyManager.Instance;
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        public void Open()
        {
            shopPanel.SetActive(true);
            Time.timeScale = 0f;
            ShowTab(0);
        }

        public void Close()
        {
            shopPanel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ //

        void ShowTab(int tab)
        {
            _activeTab = tab;
            ClearContent();

            switch (tab)
            {
                case 0: BuildToolsTab();    break;
                case 1: BuildUpgradesTab(); break;
                case 2: BuildUnlocksTab();  break;
            }
        }

        void ClearContent()
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                Destroy(contentParent.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ //
        // Tab builders
        // ------------------------------------------------------------------ //

        void BuildToolsTab()
        {
            for (int i = 0; i < allTools.Count; i++)
            {
                var data = allTools[i];
                bool owned = _toolMgr != null && _toolMgr.IsOwned(i);
                int price = data.purchaseCost;
                bool canBuy = !owned && (_currency?.Money ?? 0) >= price;

                var row = AddRow(data.toolName,
                    owned ? "Megvan" : $"$ {price}",
                    owned ? string.Empty : "Megvesz");

                if (!owned)
                {
                    int idx = i;
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                    {
                        if (_toolMgr.TryPurchase(idx)) ShowTab(0);
                    });
                }
            }
        }

        void BuildUpgradesTab()
        {
            for (int i = 0; i < allTools.Count; i++)
            {
                if (_toolMgr == null || !_toolMgr.IsOwned(i)) continue;
                var data = allTools[i];
                int level = _toolMgr.GetLevel(i);
                if (level >= 3) { AddRow(data.toolName, "Max szint", string.Empty); continue; }
                int price = data.upgradeCosts[level];
                var row = AddRow(data.toolName, $"Szint {level + 1} → {level + 2}   $ {price}", "Fejleszt");
                int idx = i;
                row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                {
                    if (_toolMgr.TryUpgrade(idx)) ShowTab(1);
                });
            }
        }

        void BuildUnlocksTab()
        {
            for (int i = 1; i < allParcels.Count; i++)
            {
                var data = allParcels[i];
                bool unlocked = _parcelMgr != null && _parcelMgr.IsUnlocked(i);
                int price = data.unlockCost;
                var row = AddRow(data.parcelName,
                    unlocked ? "Megnyitva" : $"$ {price}",
                    unlocked ? string.Empty : "Megnyit");
                if (!unlocked)
                {
                    int idx = i;
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                    {
                        if (_parcelMgr.TryUnlock(idx)) ShowTab(2);
                    });
                }
            }
        }

        // ------------------------------------------------------------------ //

        GameObject AddRow(string label, string detail, string buttonLabel)
        {
            var row = rowPrefab != null
                ? Instantiate(rowPrefab, contentParent)
                : BuildDefaultRow();

            var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) texts[0].text = label;
            if (texts.Length >= 2) texts[1].text = detail;

            var btn = row.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.gameObject.SetActive(!string.IsNullOrEmpty(buttonLabel));
                if (btn.GetComponentInChildren<TextMeshProUGUI>() is TextMeshProUGUI btnText)
                    btnText.text = buttonLabel;
            }
            return row;
        }

        GameObject BuildDefaultRow()
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(contentParent, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;

            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 44);

            MakeTMPChild("Label", row.transform, 220);
            MakeTMPChild("Detail", row.transform, 180);

            var btnGO = new GameObject("Btn", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
            btnGO.transform.SetParent(row.transform, false);
            btnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 0);
            MakeTMPChild("BtnText", btnGO.transform, 120);

            return row;
        }

        static void MakeTMPChild(string name, Transform parent, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 0);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 20; tmp.color = Color.white;
        }
    }
}

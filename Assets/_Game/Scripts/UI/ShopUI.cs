using System.Collections.Generic;
using Fields.Core.Data;
using Fields.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// In-world shop panel. Three tabs: Tools | Upgrades | Unlocks.
    /// Toggled by SaleStand.Interact(). Pauses time while open.
    /// Rows are procedurally built — rowPrefab is optional override.
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
        public GameObject rowPrefab;

        [Header("Close")]
        public Button closeButton;

        [Header("Data (assign in Inspector)")]
        public List<ToolData> allTools;
        public List<ParcelData> allParcels;

        ToolUnlockManager _toolMgr;
        ParcelManager _parcelMgr;
        CurrencyManager _currency;
        int _activeTab;

        static readonly Color COL_BG_ROW    = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        static readonly Color COL_BG_HEADER = new Color(0.06f, 0.08f, 0.12f, 1.00f);
        static readonly Color COL_BTN_BUY   = new Color(0.18f, 0.60f, 0.25f, 1.00f);
        static readonly Color COL_BTN_GREY  = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        static readonly Color COL_TEXT_MAIN = Color.white;
        static readonly Color COL_TEXT_SUB  = new Color(0.75f, 0.75f, 0.75f);
        static readonly Color COL_MONEY     = new Color(1.00f, 0.85f, 0.20f);
        static readonly Color COL_OWNED     = new Color(0.40f, 0.90f, 0.45f);
        static readonly Color COL_TAB_ON    = new Color(0.22f, 0.55f, 0.28f);
        static readonly Color COL_TAB_OFF   = new Color(0.16f, 0.16f, 0.20f);

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (tabTools)    tabTools.onClick.AddListener(() => ShowTab(0));
            if (tabUpgrades) tabUpgrades.onClick.AddListener(() => ShowTab(1));
            if (tabUnlocks)  tabUnlocks.gameObject.SetActive(false);
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (shopPanel)   shopPanel.SetActive(false);
        }

        void Start()
        {
            _toolMgr   = ToolUnlockManager.Instance;
            _parcelMgr = ParcelManager.Instance;
            _currency  = CurrencyManager.Instance;

            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDestroy()
        {
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged()
        {
            if (shopPanel != null && shopPanel.activeSelf)
                ShowTab(_activeTab);
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        public void Open()
        {
            if (shopPanel) shopPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowTab(0);
            // Dynamic UI elements need one frame before their CanvasRenderer materials are assigned.
            // WaitForEndOfFrame works even when timeScale=0.
            StartCoroutine(ForceCanvasRebuildAfterFrame());
        }

        System.Collections.IEnumerator ForceCanvasRebuildAfterFrame()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
        }

        public void Close()
        {
            if (shopPanel) shopPanel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ------------------------------------------------------------------ //

        void ShowTab(int tab)
        {
            _activeTab = tab;
            HighlightTab(tab);
            ClearContent();
            AddMoneyHeader();

            switch (tab)
            {
                case 0: BuildToolsTab();    break;
                case 1: BuildUpgradesTab(); break;
                case 2: BuildUnlocksTab();  break;
            }

            // Force layout rebuild so ContentSizeFitter updates height immediately
            if (contentParent != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    contentParent.GetComponent<RectTransform>());
                var scroll = contentParent.GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.verticalNormalizedPosition = 1f;
                Debug.Log($"[ShopUI] ShowTab({tab}) built {contentParent.childCount} rows");
            }
        }

        void HighlightTab(int tab)
        {
            SetTabColor(tabTools,    tab == 0);
            SetTabColor(tabUpgrades, tab == 1);
            SetTabColor(tabUnlocks,  tab == 2);
        }

        static void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img) img.color = active ? COL_TAB_ON : COL_TAB_OFF;
        }

        void ClearContent()
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(contentParent.GetChild(i).gameObject);
        }

        void AddMoneyHeader()
        {
            int money = _currency?.Money ?? 0;
            // Background container
            var container = new GameObject("MoneyHeader", typeof(RectTransform), typeof(Image));
            container.transform.SetParent(contentParent, false);
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 38);
            container.GetComponent<Image>().color = COL_BG_HEADER;
            // Separate child for text (Image + TMP cannot share a GameObject)
            MakeTMPChild("Text", container.transform, 0, 20, COL_MONEY, TextAlignmentOptions.Right, stretchFill: true);
            var texts = container.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
                texts[0].text = $"Egyenleg:  $ {money}";
        }

        // ------------------------------------------------------------------ //
        // Tab builders
        // ------------------------------------------------------------------ //

        static string L(string key, params object[] args) =>
            Fields.Core.LocalizationManager.Instance != null
                ? Fields.Core.LocalizationManager.Instance.Get(key, args)
                : (args.Length > 0 ? string.Format(key, args) : key);

        static string Stars(int level, int max = 3)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < level ? "*" : "-");
            return sb.ToString();
        }

        void BuildToolsTab()
        {
            for (int i = 0; i < allTools.Count; i++)
            {
                var data = allTools[i];
                bool owned = _toolMgr != null && _toolMgr.IsOwned(i);
                int price  = data.purchaseCost;

                string detail = owned
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(COL_OWNED)}>[OK] Megveve</color>"
                    : $"$ {price}";
                string btnLabel = owned ? string.Empty : L("shop.buy", price);

                var row = AddRow(data.toolName, detail, btnLabel, owned ? -1 : price);
                if (!owned)
                {
                    int idx = i;
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                    {
                        if (_toolMgr.TryPurchase(idx)) ShowTab(0);
                    });
                }
            }

            // Round baler
            var bm = Fields.Economy.BalerManager.Instance;
            if (bm?.roundBalerData != null)
            {
                AddSectionSeparator("Round Bálozo");
                bool owned = bm.RoundBalerOwned;
                int price  = bm.roundBalerData.purchaseCost;
                string detail = owned
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(COL_OWNED)}>[OK] Megveve</color>"
                    : $"$ {price}";
                var row = AddRow(L("shop.roundbaler"), detail, owned ? string.Empty : L("shop.buy", price), owned ? -1 : price);
                if (!owned)
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                    {
                        if (bm.TryPurchaseRoundBaler()) ShowTab(0);
                    });
            }
        }

        void BuildUpgradesTab()
        {
            bool anyOwned = false;
            for (int i = 0; i < allTools.Count; i++)
            {
                if (_toolMgr == null || !_toolMgr.IsOwned(i)) continue;
                anyOwned = true;
                var data  = allTools[i];
                int level = _toolMgr.GetLevel(i);
                string stars = Stars(level);

                if (level >= 3)
                {
                    AddRow($"{data.toolName}  {stars}", L("shop.maxlevel"), string.Empty, -1);
                    continue;
                }

                int price    = data.upgradeCosts[level];
                int nextLevel = level + 1;
                string detail = $"{stars}  Lv{level}→{level + 1}" +
                                $"  Spd:{data.speedLevels[nextLevel]:0.0}×" +
                                $"  Pwr:{data.powerLevels[nextLevel]:0.0}×" +
                                $"  $ {price}";
                var row = AddRow($"{data.toolName}  {stars}", detail, L("shop.upgrade", price), price);
                int idx = i;
                row.GetComponentInChildren<Button>()?.onClick.AddListener(() =>
                {
                    if (_toolMgr.TryUpgrade(idx)) ShowTab(1);
                });
            }

            if (!anyOwned)
                AddInfoRow("Nincs még megvett eszköz. Vásárolj az Eszközök fülön.");

            AddSectionSeparator("Bálozo");

            var bm = Fields.Economy.BalerManager.Instance;
            if (bm != null)
            {
                int bl = bm.BalerLevel;
                if (bl < 3)
                {
                    int cost = Fields.Economy.BalerManager.BalerUpgradeCosts[bl];
                    string detail = $"{Stars(bl)}  Lv{bl}→{bl + 1}  $ {cost}";
                    var row = AddRow(L("shop.baler"), detail, L("shop.upgrade", cost), cost);
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() => { if (bm.TryUpgradeBaler()) ShowTab(1); });
                }
                else AddRow(L("shop.baler"), $"{Stars(3)}  {L("shop.maxlevel")}", string.Empty, -1);

                AddSectionSeparator("Szénaérték");

                int hvl = bm.HayValueLevel;
                if (hvl < 3)
                {
                    float nextMult = Fields.Economy.BalerManager.HayValueMultipliers[hvl + 1];
                    int cost = Fields.Economy.BalerManager.HayValueCosts[hvl];
                    string detail = $"{Stars(hvl)}  ×{nextMult:0.00}  $ {cost}";
                    var row = AddRow(L("shop.hayvalue"), detail, L("shop.upgrade", cost), cost);
                    row.GetComponentInChildren<Button>()?.onClick.AddListener(() => { if (bm.TryUpgradeHayValue()) ShowTab(1); });
                }
                else AddRow(L("shop.hayvalue"), $"{Stars(3)}  {L("shop.maxlevel")}", string.Empty, -1);
            }
        }

        void BuildUnlocksTab()
        {
            AddInfoRow("Nincs elérhető tartalom.");
        }

        // ------------------------------------------------------------------ //
        // Row factories
        // ------------------------------------------------------------------ //

        /// <param name="price">Pass -1 for rows with no buy action (owned / max level).</param>
        GameObject AddRow(string label, string detail, string buttonLabel, int price = 0)
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
                bool hasBtn = !string.IsNullOrEmpty(buttonLabel);
                btn.gameObject.SetActive(hasBtn);
                if (hasBtn)
                {
                    if (btn.GetComponentInChildren<TextMeshProUGUI>() is TextMeshProUGUI bt)
                        bt.text = buttonLabel;

                    bool canAfford = price < 0 || (_currency != null && _currency.Money >= price);
                    if (btn.GetComponent<Image>() is Image bi)
                        bi.color = canAfford ? COL_BTN_BUY : COL_BTN_GREY;
                    btn.interactable = canAfford;
                }
            }
            return row;
        }

        void AddSectionSeparator(string title)
        {
            var container = new GameObject("Sep_" + title, typeof(RectTransform), typeof(Image));
            container.transform.SetParent(contentParent, false);
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            container.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 1f);
            MakeTMPChild("Text", container.transform, 0, 15, new Color(0.6f, 0.6f, 0.6f),
                TextAlignmentOptions.Center, stretchFill: true);
            var texts = container.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = $"— {title} —";
        }

        void AddInfoRow(string message)
        {
            var go = new GameObject("Info", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(contentParent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.6f, 0.6f, 0.6f);
        }

        GameObject BuildDefaultRow()
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(contentParent, false);
            row.GetComponent<Image>().color = COL_BG_ROW;
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 52);

            // LayoutElement so VLG uses the fixed 52px height
            var le = row.AddComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = 52;
            le.preferredHeight = 52;

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding        = new RectOffset(14, 8, 6, 6);
            hlg.spacing        = 10;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;

            MakeTMPChild("Label",  row.transform, 190, 20, COL_TEXT_MAIN);
            MakeTMPChild("Detail", row.transform, 200, 16, COL_TEXT_SUB);

            var btnGO = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(row.transform, false);
            btnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 0);
            btnGO.GetComponent<Image>().color = COL_BTN_BUY;
            MakeTMPChild("BtnText", btnGO.transform, 130, 17, COL_TEXT_MAIN, TextAlignmentOptions.Center);

            return row;
        }

        static void MakeTMPChild(string name, Transform parent, float width, float fontSize,
                                  Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
                                  bool stretchFill = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretchFill)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = new Vector2(8, 0);
                rt.offsetMax = new Vector2(-8, 0);
            }
            else
            {
                rt.sizeDelta = new Vector2(width, 0);
            }
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
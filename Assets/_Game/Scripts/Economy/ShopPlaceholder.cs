using Fields.Core.Data;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// OnGUI placeholder shop for testing — 3 tabs: Tools / Upgrades / Unlocks.
    /// Replaced by full Panel UI in P1-04.
    /// </summary>
    public class ShopPlaceholder : MonoBehaviour
    {
        [Header("Data")]
        public ToolData[] allTools = new ToolData[5];
        public ParcelData[] allParcels = new ParcelData[4];
        public BalerData squareBalerData;
        public BalerData roundBalerData;

        bool _open;
        int _tab; // 0 = Tools, 1 = Upgrades, 2 = Unlocks

        readonly string[] _tabNames = { "TOOLS", "UPGRADES", "UNLOCKS" };
        Vector2 _scroll;

        public void ToggleOpen() => _open = !_open;

        void OnGUI()
        {
            if (!_open) return;

            var style = new GUIStyle(GUI.skin.box);
            var windowRect = new Rect(Screen.width * 0.5f - 250, Screen.height * 0.5f - 200, 500, 400);
            GUI.Box(windowRect, "STAND");

            GUILayout.BeginArea(new Rect(windowRect.x + 10, windowRect.y + 25, 480, 365));

            // Money display
            GUILayout.Label($"$ {(CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : 0)}");

            // Tabs
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabNames.Length; i++)
            {
                if (GUILayout.Toggle(_tab == i, _tabNames[i], GUI.skin.button))
                    _tab = i;
            }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case 0: DrawToolsTab(); break;
                case 1: DrawUpgradesTab(); break;
                case 2: DrawUnlocksTab(); break;
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Close")) _open = false;
            GUILayout.EndArea();
        }

        void DrawToolsTab()
        {
            var um = ToolUnlockManager.Instance;
            if (um == null || allTools == null) return;
            for (int i = 0; i < allTools.Length; i++)
            {
                var data = allTools[i];
                if (data == null) continue;
                if (um.IsOwned(i))
                {
                    GUILayout.Label($"[OWNED] {data.toolName}");
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{data.toolName}  ${data.purchaseCost}");
                    if (GUILayout.Button("Buy", GUILayout.Width(60))) um.TryPurchase(i);
                    GUILayout.EndHorizontal();
                }
            }
        }

        void DrawUpgradesTab()
        {
            var um = ToolUnlockManager.Instance;
            if (um == null || allTools == null) return;
            for (int i = 0; i < allTools.Length; i++)
            {
                if (!um.IsOwned(i)) continue;
                var data = allTools[i];
                if (data == null) continue;
                int lvl = um.GetLevel(i);
                GUILayout.Label($"{data.toolName}  Lv.{lvl}");
                if (lvl < 3)
                {
                    int cost = data.upgradeCosts.Length > lvl ? data.upgradeCosts[lvl] : 999;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"  → Lv.{lvl + 1}  ${cost}");
                    if (GUILayout.Button("Upgrade", GUILayout.Width(80))) um.TryUpgrade(i);
                    GUILayout.EndHorizontal();
                }
                else GUILayout.Label("  MAX");
            }
        }

        void DrawUnlocksTab()
        {
            var pm = ParcelManager.Instance;
            if (pm == null) return;
            for (int i = 1; i < 4; i++)
            {
                var data = allParcels != null && allParcels.Length > i ? allParcels[i] : null;
                if (pm.IsUnlocked(i))
                {
                    GUILayout.Label($"[UNLOCKED] {(data != null ? data.parcelName : $"Parcel {i + 1}")}");
                }
                else
                {
                    int cost = data != null ? data.unlockCost : 0;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{(data != null ? data.parcelName : $"Parcel {i + 1}")}  ${cost}");
                    if (GUILayout.Button("Unlock", GUILayout.Width(80))) pm.TryUnlock(i);
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.Tools
{
    /// <summary>
    /// Manages the active tool slot. Handles equip/unequip and scroll/1-5 selection.
    /// Tools are child GameObjects of this holder; only the active one is enabled.
    /// </summary>
    public class ToolHolder : MonoBehaviour
    {
        [Header("Tool Slots (assign in inspector, ordered 1-5)")]
        public List<BaseTool> tools = new List<BaseTool>(5);

        [Header("Default slot equipped on Start (0-based)")]
        public int defaultToolIndex = 0;

        /// <summary>Fired on local player: (toolIndex, isPressed). Consumed by NetworkedPlayer to send CmdUseTool.</summary>
        public event Action<int, bool> OnToolAction;

        int _activeIndex = -1;

        void Start()
        {
            // Push current unlock levels into tool components immediately
            ApplyUpgradeLevels();

            foreach (var t in tools) t?.OnUnequip();
            if (tools.Count > 0) EquipSlot(Mathf.Clamp(defaultToolIndex, 0, tools.Count - 1));

            var um = Fields.Economy.ToolUnlockManager.Instance;
            if (um != null)
                um.OnToolUpgraded += OnToolUpgraded;
        }

        void OnDestroy()
        {
            var um = Fields.Economy.ToolUnlockManager.Instance;
            if (um != null)
                um.OnToolUpgraded -= OnToolUpgraded;
        }

        void OnToolUpgraded(int toolIndex, int newLevel)
        {
            if (toolIndex >= 0 && toolIndex < tools.Count && tools[toolIndex] != null)
                tools[toolIndex].upgradeLevel = newLevel;
        }

        void ApplyUpgradeLevels()
        {
            var um = Fields.Economy.ToolUnlockManager.Instance;
            if (um == null) return;
            for (int i = 0; i < tools.Count; i++)
                if (tools[i] != null)
                    tools[i].upgradeLevel = um.GetLevel(i);
        }

        // ------------------------------------------------------------------ //
        // Input callbacks (wired via PlayerInput)
        // ------------------------------------------------------------------ //

        public void OnToolSelect(InputValue value)
        {
            // Numeric keys 1-5 send 1.0–5.0 as float
            int slot = Mathf.RoundToInt(value.Get<float>()) - 1;
            if (slot >= 0 && slot < tools.Count) EquipSlot(slot);
        }

        public void OnScrollTool(InputValue value)
        {
            float scroll = value.Get<float>();
            if (Mathf.Abs(scroll) < 0.01f) return;
            int dir = scroll > 0 ? 1 : -1;
            int next = _activeIndex;
            for (int i = 0; i < tools.Count; i++)
            {
                next = (next + dir + tools.Count) % tools.Count;
                if (IsOwned(next)) break;
            }
            EquipSlot(next);
        }

        public void OnUsePrimary(InputValue value)
        {
            if (Time.timeScale == 0f) return; // shop/pause is open
            // Block tool use during baling or while carrying bales
            var player = Fields.Core.PlayerController.Instance;
            if (player != null && (player.IsBaling || player.CarriedBaleCount > 0)) return;
            bool pressed = value.isPressed;
            ActiveTool?.OnUsePrimary(pressed);
            OnToolAction?.Invoke(_activeIndex, pressed);
        }

        /// <summary>Remote replay: equip slot then trigger use — called by NetworkedPlayer RpcUseTool.</summary>
        public void ForceUsePrimary(int toolIndex, bool pressed)
        {
            if (toolIndex >= 0 && toolIndex < tools.Count && toolIndex != _activeIndex)
                EquipSlot(toolIndex);
            ActiveTool?.OnUsePrimary(pressed);
        }

        // ------------------------------------------------------------------ //

        void EquipSlot(int index)
        {
            if (index == _activeIndex) return;
            if (!IsOwned(index)) return;

            if (_activeIndex >= 0 && _activeIndex < tools.Count)
                tools[_activeIndex]?.OnUnequip();

            _activeIndex = index;
            ActiveTool?.OnEquip();

            var tip = ActiveTool?.ToolTip;
            if (!string.IsNullOrEmpty(tip))
                Fields.UI.HUDController.Instance?.ShowToolTip(tip, 2.5f);
        }

        bool IsOwned(int index)
        {
            if (index >= 0 && index < tools.Count && tools[index] is BareHand) return true;
            var mgr = Fields.Economy.ToolUnlockManager.Instance;
            return mgr == null || mgr.IsOwned(index);
        }

        public void EquipBareHand()
        {
            // Find BareHand by type — don't assume slot 0
            for (int i = 0; i < tools.Count; i++)
                if (tools[i] is BareHand) { EquipSlot(i); return; }
            EquipSlot(0); // fallback
        }

        public void RefuelAllPowered()
        {
            foreach (var t in tools)
                if (t is PoweredToolBase pt) pt.RefuelFull();
        }

        public BaseTool ActiveTool =>
            _activeIndex >= 0 && _activeIndex < tools.Count ? tools[_activeIndex] : null;

        public int ActiveIndex => _activeIndex;
    }
}

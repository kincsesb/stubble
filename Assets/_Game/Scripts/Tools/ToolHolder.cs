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

        int _activeIndex = -1;

        void Start()
        {
            foreach (var t in tools) t?.OnUnequip();
            if (tools.Count > 0) EquipSlot(0);
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
            int next = _activeIndex + (scroll > 0 ? 1 : -1);
            next = (next + tools.Count) % tools.Count;
            EquipSlot(next);
        }

        public void OnUsePrimary(InputValue value)
        {
            ActiveTool?.OnUsePrimary(value.isPressed);
        }

        // ------------------------------------------------------------------ //

        void EquipSlot(int index)
        {
            if (index == _activeIndex) return;
            if (_activeIndex >= 0 && _activeIndex < tools.Count)
                tools[_activeIndex]?.OnUnequip();

            _activeIndex = index;
            ActiveTool?.OnEquip();
        }

        public BaseTool ActiveTool =>
            _activeIndex >= 0 && _activeIndex < tools.Count ? tools[_activeIndex] : null;

        public int ActiveIndex => _activeIndex;
    }
}

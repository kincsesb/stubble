using System.Collections.Generic;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Singleton — aktiválja a cheat kódokat, kezeli az időzített effekteket.
    /// CheatCodeTerminal.TryActivate()-ot hív, PlayerController és PoweredToolBase
    /// ezt kérdezi az IsIddqdActive property-n keresztül.
    /// </summary>
    public class CheatCodeActivator : MonoBehaviour
    {
        public static CheatCodeActivator Instance { get; private set; }

        public bool IsIddqdActive { get; private set; }

        readonly HashSet<string> _activated = new();

        float _iddqdTimer;
        bool _lowGravityActive;
        float _lowGravityTimer;
        Vector3 _defaultGravity;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _defaultGravity = Physics.gravity;
        }

        void Update()
        {
            if (IsIddqdActive)
            {
                _iddqdTimer -= Time.deltaTime;
                // Keep stamina full each frame
                var pc = PlayerController.Instance;
                if (pc != null)
                {
                    pc.RefillStamina();
                }
                if (_iddqdTimer <= 0f)
                {
                    IsIddqdActive = false;
                    _activated.Remove("iddqd");
                    SteamManager.Instance?.UnlockAchievement(SteamManager.Achievements.IT_WAS_MORTAL);
                    Toast("IDDQD EXPIRED. YOU ARE MORTAL AGAIN.", 4f);
                }
            }

            if (_lowGravityActive)
            {
                _lowGravityTimer -= Time.deltaTime;
                if (_lowGravityTimer <= 0f)
                {
                    _lowGravityActive = false;
                    Physics.gravity = _defaultGravity;
                    _activated.Remove("lowgravity");
                    Toast("GRAVITY RESTORED. WELCOME BACK TO EARTH.", 3f);
                }
            }
        }

        /// <summary>
        /// Megpróbál aktiválni egy cheat kódot.
        /// Visszatér: (siker, terminál szöveg).
        /// </summary>
        public (bool ok, string response) TryActivate(string raw)
        {
            string code = raw.Trim().ToLower();

            if (string.IsNullOrEmpty(code))
                return (false, "SYNTAX ERROR: EMPTY INPUT");

            if (_activated.Contains(code))
                return (false, $"ERROR: {code.ToUpper()} ALREADY ACTIVE");

            switch (code)
            {
                case "hesoyam":
                    Earn(50000, SteamManager.Achievements.HESOYAM);
                    _activated.Add(code);
                    return (true, "CJ SAYS: SWEEEEET. +$50,000");

                case "motherlode":
                    Earn(50000, SteamManager.Achievements.MOTHERLODE);
                    _activated.Add(code);
                    return (true, "SIMS ENERGY DETECTED. +$50,000. MOTIVE: MONEY [SATISFIED]");

                case "iddqd":
                    IsIddqdActive = true;
                    _iddqdTimer = 300f;
                    _activated.Add(code);
                    SteamManager.Instance?.UnlockAchievement(SteamManager.Achievements.DOOM);
                    return (true, "RIP AND TEAR. INFINITE STAMINA + FUEL. (5:00 MIN)");

                case "rosebud":
                    Earn(1000, null);
                    _activated.Add(code);
                    return (true, "+$1,000. (MOTHERLODE GIVES $50K. JUST SAYING.)");

                case "lowgravity":
                    _lowGravityActive = true;
                    _lowGravityTimer = 30f;
                    Physics.gravity = new Vector3(0f, -1.62f, 0f);
                    _activated.Add(code);
                    return (true, "ONE SMALL STEP FOR FARMER. (30 SECONDS)");

                case "xyzzy":
                    Earn(1, null);
                    _activated.Add(code);
                    return (true, "A HOLLOW VOICE SAYS 'PLUGH'. +$1.");

                default:
                    return (false, null); // terminal handles escalating failure msgs
            }
        }

        void Earn(int amount, string achievement)
        {
            if (amount > 0) Fields.Economy.CurrencyManager.Instance?.Earn(amount);
            if (achievement != null) SteamManager.Instance?.UnlockAchievement(achievement);
        }

        void Toast(string msg, float dur) =>
            Fields.UI.HUDController.Instance?.ShowToolTip(msg, dur);
    }
}

using Fields.Core;
using Fields.Core.Data;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Baler machine — player feeds HayPiles in, bale is ejected after threshold.
    /// Supports square (stack-carry) and round (rolling physics) bales per BalerData.
    /// Compression sequence: loading → full-compression → "thunk" feel → eject.
    /// </summary>
    public class Baler : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        public BalerData balerData;
        [Tooltip("When round baler is purchased, this data overrides balerData")]
        public BalerData roundBalerData;
        public int upgradeLevel;

        [Header("Spawn")]
        [Tooltip("Point where the finished bale is ejected")]
        public Transform ejectPoint;
        public GameObject squareBalePrefab;
        public GameObject roundBalePrefab;

        [Header("Feel")]
        public MMF_Player feedbackThunk;
        public MMF_Player feedbackEject;

        /// <summary>
        /// Co-op hook: set by EconomyNetSync on non-host clients.
        /// Returns true → intercepted (host will spawn via NetworkServer.Spawn).
        /// </summary>
        public System.Func<Vector3, Quaternion, GameObject, bool> SpawnBaleInterceptor;

        float _hayAccumulated;
        bool _compressing;
        float _compressionTimer;

        // Switches to round baler mode when the player has purchased it (spec §7.2).
        BalerData ActiveBalerData
        {
            get
            {
                var bm = Fields.Economy.BalerManager.Instance;
                return bm != null && bm.RoundBalerOwned && roundBalerData != null
                    ? roundBalerData : balerData;
            }
        }

        int ActiveLevel => Fields.Economy.BalerManager.Instance?.BalerLevel ?? upgradeLevel;

        float HayRequired
        {
            get
            {
                var d = ActiveBalerData;
                return d != null && d.densityLevels.Length > ActiveLevel
                    ? d.densityLevels[ActiveLevel] : 60f;
            }
        }

        // ------------------------------------------------------------------ //
        // IInteractable — player brings HayPile
        // ------------------------------------------------------------------ //

        public void Interact(Fields.Core.PlayerController player)
        {
            // Block only if carrying finished square bales (not loose hay piles)
            if (player.GetCarriedSquareBales().Count > 0) return;

            // Search for hay pile: carried by player counts (player is standing here)
            var pile = FindNearestHayPile(player.transform.position);
            if (pile == null) return;

            // If the pile was carried by this player, remove it from their carry list
            if (pile.IsCarried)
                player.RemoveCarriedHayPile(pile);

            float hayUnits = pile.HayUnits;
            pile.ConsumeAll();

            _hayAccumulated += hayUnits;

            if (_hayAccumulated >= HayRequired && !_compressing)
                StartCompression();
        }

        // ------------------------------------------------------------------ //

        void StartCompression()
        {
            _compressing = true;
            int level = ActiveLevel;
            var d = ActiveBalerData;
            float speed = d != null && d.compressionSpeedLevels.Length > level
                ? d.compressionSpeedLevels[level]
                : 1f;
            _compressionTimer = 2f / speed;
            Fields.Audio.ToolAudioManager.Instance?.StartBaler();
        }

        void Update()
        {
            if (!_compressing) return;

            _compressionTimer -= Time.deltaTime;
            if (_compressionTimer <= 0f)
                EjectBale();
        }

        int _totalBalesEjected;

        void EjectBale()
        {
            _compressing = false;
            _hayAccumulated -= HayRequired;
            _totalBalesEjected++;
            Fields.Audio.ToolAudioManager.Instance?.StopBaler();

            // Feel
            feedbackThunk?.PlayFeedbacks(transform.position);

            // Spawn bale
            bool isRound = ActiveBalerData?.isRoundBaler == true;
            var prefab = isRound ? roundBalePrefab : squareBalePrefab;
            if (prefab == null) { Debug.LogWarning("[Baler] No bale prefab assigned"); return; }

            var spawnPos = ejectPoint != null ? ejectPoint.position : transform.position + transform.forward;
            var spawnRot = ejectPoint != null ? ejectPoint.rotation : Quaternion.identity;

            if (SpawnBaleInterceptor == null || !SpawnBaleInterceptor(spawnPos, spawnRot, prefab))
                Instantiate(prefab, spawnPos, spawnRot);

            feedbackEject?.PlayFeedbacks(spawnPos);

            // Steam achievements
            var steam = Fields.Core.SteamManager.Instance;
            if (steam != null)
            {
                if (_totalBalesEjected == 1)  steam.OnFirstBale();
                if (isRound)                  steam.OnRoundBale();
                if (_totalBalesEjected == 10) steam.UnlockAchievement(Fields.Core.SteamManager.Achievements.TEN_BALES);
                if (_totalBalesEjected == 100) steam.UnlockAchievement(Fields.Core.SteamManager.Achievements.HUNDRED_BALES);
            }

            if (_hayAccumulated >= HayRequired)
                StartCompression();
        }

        // Search from baler OR from player position (covers carried piles).
        HayPile FindNearestHayPile(UnityEngine.Vector3? playerPos = null)
        {
            var piles = Object.FindObjectsByType<HayPile>(FindObjectsSortMode.None);
            HayPile best = null;
            float bestDist = float.MaxValue;
            foreach (var p in piles)
            {
                if (p.HayUnits <= 0f) continue;
                // Prefer baler-proximity for world piles; use player position for carried ones
                var origin = (p.IsCarried && playerPos.HasValue) ? playerPos.Value : transform.position;
                float d = (p.transform.position - origin).magnitude;
                // World piles: must be within 8m of baler. Carried piles: always accepted.
                float limit = p.IsCarried ? float.MaxValue : 8f;
                if (d < limit && d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ //

        public float CompressionProgress =>
            _compressing && _compressionTimer > 0
                ? 1f - (_compressionTimer / 2f)
                : (_hayAccumulated / HayRequired);

        public bool IsCompressing => _compressing;
    }
}

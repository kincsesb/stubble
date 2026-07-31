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
        public int upgradeLevel;

        [Header("Spawn")]
        [Tooltip("Point where the finished bale is ejected")]
        public Transform ejectPoint;
        public GameObject squareBalePrefab;
        public GameObject roundBalePrefab;

        [Header("Feel")]
        public MMF_Player feedbackThunk;
        public MMF_Player feedbackEject;

        float _hayAccumulated;
        float _hayRequired;
        bool _compressing;
        float _compressionTimer;

        // ------------------------------------------------------------------ //

        void Start()
        {
            RefreshThreshold();
        }

        void RefreshThreshold()
        {
            if (balerData == null) { _hayRequired = 60f; return; }
            // hayRequired scales with compression speed upgrade (higher = faster = less hay per bale)
            _hayRequired = 60f; // fixed: 60 hay units = 1 bale (spec)
        }

        // ------------------------------------------------------------------ //
        // IInteractable — player brings HayPile
        // ------------------------------------------------------------------ //

        public void Interact(Fields.Core.PlayerController player)
        {
            if (player.CarriedBaleCount > 0) return; // carrying bales, not hay

            // Try to take a HayPile from the player's vicinity
            var pile = FindNearestHayPile();
            if (pile == null) return;

            float hayUnits = pile.HayUnits;
            pile.ConsumeAll();

            _hayAccumulated += hayUnits;

            if (_hayAccumulated >= _hayRequired && !_compressing)
                StartCompression();
        }

        // ------------------------------------------------------------------ //

        void StartCompression()
        {
            _compressing = true;
            float speed = balerData != null && balerData.compressionSpeedLevels.Length > upgradeLevel
                ? balerData.compressionSpeedLevels[upgradeLevel]
                : 1f;
            _compressionTimer = 2f / speed; // base 2 seconds, halved at max upgrade
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
            _hayAccumulated -= _hayRequired;
            _totalBalesEjected++;

            // Feel
            feedbackThunk?.PlayFeedbacks(transform.position);

            // Spawn bale
            bool isRound = balerData != null && balerData.isRoundBaler;
            var prefab = isRound ? roundBalePrefab : squareBalePrefab;
            if (prefab == null) { Debug.LogWarning("[Baler] No bale prefab assigned"); return; }

            var spawnPos = ejectPoint != null ? ejectPoint.position : transform.position + transform.forward;
            var spawnRot = ejectPoint != null ? ejectPoint.rotation : Quaternion.identity;

            // P2-04: co-op host must call NetworkServer.Spawn() here — deferred to Mirror integration
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

            // Start another compression if enough hay is still buffered
            if (_hayAccumulated >= _hayRequired)
                StartCompression();
        }

        HayPile FindNearestHayPile()
        {
            var piles = Object.FindObjectsByType<HayPile>(FindObjectsSortMode.None);
            HayPile best = null; float bestDist = 4f; // max 4m radius
            foreach (var p in piles)
            {
                float d = (p.transform.position - transform.position).magnitude;
                if (d < bestDist && p.HayUnits > 0f) { bestDist = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ //

        public float CompressionProgress =>
            _compressing && _compressionTimer > 0
                ? 1f - (_compressionTimer / 2f)
                : (_hayAccumulated / _hayRequired);

        public bool IsCompressing => _compressing;
    }
}

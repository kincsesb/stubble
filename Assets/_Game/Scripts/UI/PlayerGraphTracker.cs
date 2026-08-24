using System.Collections.Generic;
using Fields.Core;
using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Records per-player cumulative cut-area samples over time so the EndScreen
    /// can draw a "time vs cut grass" line graph. Creates itself at scene load.
    /// Samples every SAMPLE_INTERVAL seconds (real time, unaffected by timeScale).
    /// </summary>
    public class PlayerGraphTracker : MonoBehaviour
    {
        public static PlayerGraphTracker Instance { get; private set; }

        public struct Sample { public float TimeSec; public float AreaM2; }

        const float SAMPLE_INTERVAL = 30f;
        const float CELL_AREA_M2    = 0.16f; // 0.4m × 0.4m

        readonly Dictionary<int, List<Sample>> _data = new Dictionary<int, List<Sample>>();
        float _timer;
        float _startTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[PlayerGraphTracker]");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerGraphTracker>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance  = this;
            _startTime = Time.realtimeSinceStartup;
            Record(forceRecord: true);
        }

        void OnEnable()  => GameEvents.OnFullGameCompleted += OnGameComplete;
        void OnDisable() => GameEvents.OnFullGameCompleted -= OnGameComplete;

        void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= SAMPLE_INTERVAL) { _timer = 0f; Record(); }
        }

        void OnGameComplete(float _) => Record();

        void Record(bool forceRecord = false)
        {
            var ss = SessionState.Instance;
            if (ss == null) return;
            float t = Time.realtimeSinceStartup - _startTime;
            foreach (var p in ss.Players)
            {
                if (p == null) continue;
                if (!_data.ContainsKey(p.PlayerId))
                    _data[p.PlayerId] = new List<Sample> { new Sample { TimeSec = 0f, AreaM2 = 0f } };
                var list = _data[p.PlayerId];
                float area = p.AreaCutCells * CELL_AREA_M2;
                if (!forceRecord && list.Count > 0 && list[list.Count - 1].AreaM2 == area) continue;
                list.Add(new Sample { TimeSec = t, AreaM2 = area });
            }
        }

        /// <summary>Returns a copy of all recorded samples for the given player.</summary>
        public Sample[] GetSamples(int playerId)
        {
            Record();
            return _data.TryGetValue(playerId, out var list) ? list.ToArray() : new Sample[0];
        }

        public void Reset()
        {
            _data.Clear();
            _startTime = Time.realtimeSinceStartup;
            _timer     = 0f;
        }
    }
}

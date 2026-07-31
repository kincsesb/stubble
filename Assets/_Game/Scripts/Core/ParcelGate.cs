using Fields.Economy;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Gate between two parcels. Opens (slides down) when the target parcel is unlocked.
    /// ParcelManager calls OpenGate() after purchase; no animation system needed in P0.
    /// </summary>
    public class ParcelGate : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Index of the parcel this gate guards (0-based, matches ParcelManager.parcels)")]
        public int guardedParcelIndex = 1;

        [Tooltip("Distance the gate slides down when opened")]
        public float slideDistance = 3f;

        [Tooltip("Speed at which the gate slides (units/sec)")]
        public float slideSpeed = 2f;

        bool _open;
        bool _sliding;
        Vector3 _closedPos;
        Vector3 _openPos;

        void Awake()
        {
            _closedPos = transform.position;
            _openPos   = _closedPos + Vector3.down * slideDistance;
        }

        void Start()
        {
            // Subscribe to ParcelManager if available
            if (ParcelManager.Instance != null)
                ParcelManager.Instance.OnParcelUnlocked += OnParcelUnlocked;
        }

        void OnDestroy()
        {
            if (ParcelManager.Instance != null)
                ParcelManager.Instance.OnParcelUnlocked -= OnParcelUnlocked;
        }

        void OnParcelUnlocked(int parcelIndex)
        {
            if (parcelIndex == guardedParcelIndex) OpenGate();
        }

        public void OpenGate()
        {
            if (_open) return;
            _open    = true;
            _sliding = true;
        }

        void Update()
        {
            if (!_sliding) return;
            transform.position = Vector3.MoveTowards(
                transform.position, _openPos, slideSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, _openPos) < 0.01f)
            {
                transform.position = _openPos;
                _sliding = false;
            }
        }

        public bool IsOpen => _open;
    }
}

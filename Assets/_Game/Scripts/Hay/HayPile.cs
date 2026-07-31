using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// A physical hay pile in the world. In co-op this is a NetworkObject (added in P2).
    /// Implements the IPickupable interface for player carry.
    /// </summary>
    public class HayPile : MonoBehaviour, IPickupable
    {
        [Tooltip("Visual size variant: 0=small, 1=medium, 2=large")]
        public int sizeVariant;
        public int hayUnits;

        bool _isCarried;
        Transform _carrier;

        public bool IsCarried => _isCarried;

        // ------------------------------------------------------------------ //
        // IPickupable
        // ------------------------------------------------------------------ //

        public bool CanPickup(Transform byPlayer) => !_isCarried;

        public void OnPickup(Transform carrier)
        {
            _isCarried = true;
            _carrier = carrier;
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = Vector3.zero;

            if (TryGetComponent<Collider>(out var col))
                col.enabled = false;
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;
        }

        public void OnDrop(Vector3 worldPosition)
        {
            _isCarried = false;
            _carrier = null;
            transform.SetParent(null);
            transform.position = worldPosition;

            if (TryGetComponent<Collider>(out var col))
                col.enabled = true;
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = false;
        }

        public float HayUnits => hayUnits;

        public void ConsumeAll()
        {
            hayUnits = 0;
            Destroy(gameObject);
        }
    }

    public interface IPickupable
    {
        bool CanPickup(Transform byPlayer);
        void OnPickup(Transform carrier);
        void OnDrop(Vector3 worldPosition);
    }
}
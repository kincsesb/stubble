using Fields.Core;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Square bale — stackable, player carries 1/2/3.
    /// Intentionally occludes view when carrying (spec §8.1.2).
    /// Collides with player only to push, never topple (anti-pattern #7).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class SquareBale : MonoBehaviour, IPickupable, IInteractable
    {
        [Header("Stack")]
        [Tooltip("How many hay units this bale contains")]
        public int hayUnits = 60;
        [Tooltip("Stack index when player holds multiple (0 = bottom)")]
        public int stackIndex;

        Rigidbody _rb;
        bool _isCarried;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        // ------------------------------------------------------------------ //
        // IPickupable
        // ------------------------------------------------------------------ //

        public bool CanPickup(Transform byPlayer)
        {
            if (_isCarried) return false;
            var player = byPlayer.GetComponentInParent<PlayerController>()
                      ?? byPlayer.GetComponent<PlayerController>();
            return player == null || player.CarriedBaleCount < 3;
        }

        public void OnPickup(Transform carrier)
        {
            _isCarried = true;
            _rb.isKinematic = true;
            GetComponent<Collider>().enabled = false;
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
        }

        public void OnDrop(Vector3 worldPosition)
        {
            _isCarried = false;
            transform.SetParent(null);
            transform.position = worldPosition;
            _rb.isKinematic = false;
            GetComponent<Collider>().enabled = true;
        }

        // ------------------------------------------------------------------ //
        // IInteractable — player picks up on Interact press
        // ------------------------------------------------------------------ //

        public void Interact(PlayerController player)
        {
            if (_isCarried) return;
            if (player.CarriedBaleCount >= 3) return;
            player.PickupBale(this);
        }

        // ------------------------------------------------------------------ //

        public bool IsCarried => _isCarried;
        public int HayUnits => hayUnits;
    }
}

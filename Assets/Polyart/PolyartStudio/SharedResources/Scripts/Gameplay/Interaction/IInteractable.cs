using UnityEngine;

namespace Polyart
{
    public struct InteractionData
    {
        public RaycastHit hit;
        public GameObject interactor;
    }

    public interface IInteractable
    {
        void Interact(InteractionData data);
    }
}

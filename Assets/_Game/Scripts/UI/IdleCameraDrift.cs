using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Slowly orbits the camera around a look target while the main menu is visible.
    /// Attach to the menu camera GameObject. Disable the component (or the GameObject)
    /// when gameplay starts — the player camera takes over.
    /// </summary>
    public class IdleCameraDrift : MonoBehaviour
    {
        [Header("Orbit")]
        public Transform lookTarget;
        public float orbitRadius = 8f;
        public float orbitSpeed  = 3f;     // degrees per second
        public float heightOffset = 1.8f;

        [Header("Height sway")]
        public float swayAmplitude = 0.3f;
        public float swaySpeed     = 0.15f;

        float _angle;

        void OnEnable()
        {
            // Start from current yaw so the camera doesn't snap on enable.
            var delta = transform.position - (lookTarget != null ? lookTarget.position : Vector3.zero);
            _angle = Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg;
        }

        void Update()
        {
            _angle += orbitSpeed * Time.unscaledDeltaTime;

            float rad    = _angle * Mathf.Deg2Rad;
            var   target = lookTarget != null ? lookTarget.position : Vector3.zero;
            var   offset = new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                heightOffset + Mathf.Sin(Time.unscaledTime * swaySpeed) * swayAmplitude,
                Mathf.Sin(rad) * orbitRadius);

            transform.position = target + offset;
            transform.LookAt(target + Vector3.up * 0.6f);
        }
    }
}
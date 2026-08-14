using UnityEngine;

namespace Fields.Hay
{
    [CreateAssetMenu(fileName = "RoundBaleConfig", menuName = "Fields/Round Bale Config")]
    public class RoundBaleConfig : ScriptableObject
    {
        [Header("Geometry")]
        public float diameter = 1.5f;
        public float width = 1.2f;
        [Tooltip("Which Euler axis to use for roll rotation on the Visual child. 0=X 1=Y 2=Z — verify visually.")]
        public int rollMeshAxis = 0;

        [Header("Physics")]
        public float mass = 220f;
        public float pushAcceleration = 2.5f;
        public float speedCap = 10f;
        [Tooltip("Quadratic damping activates above this speed")]
        public float quadDampThreshold = 8f;
        public float quadDampCoeff = 2f;

        [Header("Rolling Resistance (m/s²)")]
        public float resistanceUncutGrass = 1.6f;
        public float resistanceCutStubble = 1.1f;
        public float resistanceDirt = 1.3f;
        public float resistanceGravel = 2.0f;

        [Header("Steering")]
        [Tooltip("Max steer rate at standstill (deg/s)")]
        public float maxSteerRate = 90f;
        [Tooltip("Speed at which steer rate reaches zero (m/s)")]
        public float steerSpeedThreshold = 3f;

        [Header("Ground Alignment")]
        [Tooltip("Lerp rate toward target ground height (units/s)")]
        public float groundAlignRate = 8f;

        [Header("Collisions")]
        [Tooltip("dot(heading, contactNormal) < this → head-on. Spec default: -0.7")]
        public float headOnDotThreshold = -0.7f;
        [Tooltip("Fraction of speed lost on head-on impact")]
        [Range(0f, 1f)] public float headOnSpeedLoss = 0.85f;
        [Tooltip("Fraction of speed transferred to a struck bale")]
        [Range(0f, 1f)] public float baleToBaleTransfer = 0.6f;
        [Tooltip("Seconds lateral constraint is relaxed after a collision")]
        public float postCollisionExemptTime = 0.2f;

        [Header("Feel — Camera / Haptics")]
        public float cameraShakeProximity = 3f;
        public float cameraShakeScalePerSpeed = 0.003f;
        public float hapticRumbleScale = 1f;

        public float Radius => diameter * 0.5f;
        public float Circumference => Mathf.PI * diameter;
    }
}

using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Base for powered tools (string trimmer, push mower, ride-on).
    /// Uses a fuel pool instead of stamina swing cycles.
    /// </summary>
    public abstract class PoweredToolBase : BaseTool
    {
        [Header("Powered Tool")]
        public float fuelCapacity = 100f;
        public float fuelConsumptionPerSecond = 5f;

        protected float _fuel;
        protected bool _engineRunning;

        public override void OnEquip()
        {
            base.OnEquip();
            _fuel = fuelCapacity;
        }

        protected void StartEngine()
        {
            // TODO: re-enable fuel check when fuel system is ready
            // if (_fuel <= 0f) return;
            _engineRunning = true;
            OnEngineStarted();
        }

        protected void StopEngine()
        {
            _engineRunning = false;
            OnEngineStopped();
        }

        protected virtual void Update()
        {
            if (!_isEquipped || !_engineRunning) return;
            // TODO: re-enable fuel drain when fuel system is ready
            // _fuel = Mathf.Max(0f, _fuel - fuelConsumptionPerSecond * Time.deltaTime);
            // if (_fuel <= 0f) StopEngine();
        }

        protected virtual void OnEngineStarted() { }
        protected virtual void OnEngineStopped() { }

        public void RefuelFull() => _fuel = fuelCapacity;

        public float FuelNormalized => fuelCapacity > 0 ? _fuel / fuelCapacity : 0f;
        public bool IsRunning => _engineRunning;
    }
}
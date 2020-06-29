using System;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public abstract class CameraLabeler
    {
        public bool enabled;
        public bool foldout;

        internal bool isInitialized { get; private set; }

        protected PerceptionCamera perceptionCamera { get; private set; }
        protected SensorHandle SensorHandle { get; private set; }

        public virtual void Setup() { }
        public virtual void OnUpdate() {}
        public virtual void OnBeginRendering() {}

        public virtual void Cleanup() {}

        internal void Init(PerceptionCamera perceptionCamera)
        {
            try
            {
                this.perceptionCamera = perceptionCamera;
                SensorHandle = perceptionCamera.SensorHandle;
                Setup();
                isInitialized = true;
            }
            catch (Exception)
            {
                this.enabled = false;
                throw;
            }
        }
    }
}

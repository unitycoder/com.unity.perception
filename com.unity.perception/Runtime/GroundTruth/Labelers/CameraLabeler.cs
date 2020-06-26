using System;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public abstract class CameraLabeler
    {
        public bool enabled;
        public bool foldout;

        protected PerceptionCamera PerceptionCamera { get; private set; }
        protected SensorHandle SensorHandle { get; private set; }

        public abstract void Setup();
        public virtual void OnUpdate() {}
        public virtual void OnBeginRendering() {}

        public virtual void OnSimulationEnding() {}

        internal void Init(PerceptionCamera perceptionCamera)
        {
            PerceptionCamera = perceptionCamera;
            SensorHandle = perceptionCamera.SensorHandle;
        }
    }
}

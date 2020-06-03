using System;

namespace UnityEngine.Perception.GroundTruth {
    [Serializable]
    public class SemanticSegmentationLabeler : CameraLabeler
    {
        public string annotationId = "12F94D8D-5425-4DEB-9B21-5E53AD957D66";
        public SemanticSegmentationLabelConfig labelConfig;
    }
}

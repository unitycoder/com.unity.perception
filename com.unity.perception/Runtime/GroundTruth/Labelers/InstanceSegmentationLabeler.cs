using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace UnityEngine.Perception.GroundTruth {
    // public class InstanceSegmentationLabeler : CameraLabeler
    // {
    //     //Uncomment when we support saving instance segmentation labels
    //     //public bool saveImages = false;
    //     //public string annotationId = "E657461D-B950-42E1-8141-BEC9B4810241";
    //
    //     public override void Setup()
    //     {
    //         PerceptionCamera.InstanceSegmentationImageReadback += OnInstanceSegmentationImageReadback;
    //     }
    //
    //     void OnInstanceSegmentationImageReadback(int frameCount, NativeArray<uint> imageData, RenderTexture renderTexture)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }
}

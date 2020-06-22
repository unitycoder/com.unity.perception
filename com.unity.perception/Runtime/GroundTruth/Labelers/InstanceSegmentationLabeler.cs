using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace UnityEngine.Perception.GroundTruth {
    [AddComponentMenu("Perception/Labelers/InstanceSegmentationLabeler")]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(PerceptionCamera))]
    public class InstanceSegmentationLabeler : MonoBehaviour
    {
        //Uncomment when we support saving instance segmentation labels
        //public bool saveImages = false;
        //public string annotationId = "E657461D-B950-42E1-8141-BEC9B4810241";

        RenderTexture m_SegmentationTexture;
        RenderTextureReader<uint> m_SegmentationReader;
#if HDRP_PRESENT
        InstanceSegmentationPass m_SegmentationPass;
#endif
#if URP_PRESENT
        [NonSerialized]
        InstanceSegmentationUrpPass m_InstanceSegmentationUrpPass;
#endif

        public RenderTexture InstanceSegmentationRenderTexture => m_SegmentationTexture;

        public event Action<int, NativeArray<uint>, RenderTexture> InstanceSegmentationImageReadback;

        public void Start()
        {
            var myCamera = GetComponent<Camera>();
            var perceptionCamera = GetComponent<PerceptionCamera>();
            var width = myCamera.pixelWidth;
            var height = myCamera.pixelHeight;
            m_SegmentationTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            m_SegmentationTexture.name = "Segmentation";

#if HDRP_PRESENT
            var customPassVolume = this.GetComponent<CustomPassVolume>() ?? gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            customPassVolume.isGlobal = true;
            m_SegmentationPass = new InstanceSegmentationPass()
            {
                name = "Segmentation Pass",
                targetCamera = myCamera,
                targetTexture = m_SegmentationTexture
            };
            m_SegmentationPass.EnsureInit();
            customPassVolume.customPasses.Add(m_SegmentationPass);
#endif
#if URP_PRESENT
            perceptionCamera.AddScriptableRenderPass(new InstanceSegmentationUrpPass(myCamera, m_SegmentationTexture));
#endif

            m_SegmentationReader = new RenderTextureReader<uint>(m_SegmentationTexture, myCamera, (frameCount, data, tex) =>
            {
                InstanceSegmentationImageReadback?.Invoke(frameCount, data, tex);
            });
        }

        public void OnDisable()
        {
            if (m_SegmentationTexture != null)
                m_SegmentationTexture.Release();

            m_SegmentationTexture = null;

            m_SegmentationReader?.WaitForAllImages();
            m_SegmentationReader?.Dispose();
            m_SegmentationReader = null;
        }
    }
}

using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif
#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEngine.Perception.GroundTruth
{
    partial class PerceptionCamera
    {
        public event Action<int, NativeArray<uint>, RenderTexture> InstanceSegmentationImageReadback;

        /// <summary>
        /// Invoked when RenderedObjectInfos are calculated. The first parameter is the Time.frameCount at which the objects were rendered. This may be called many frames after the frame in which the objects were rendered.
        /// </summary>
        public event Action<int, NativeArray<RenderedObjectInfo>> RenderedObjectInfosCalculated;

        RenderedObjectInfoGenerator m_RenderedObjectInfoGenerator;
        RenderTexture m_InstanceSegmentationTexture;
        RenderTextureReader<uint> m_InstanceSegmentationReader;
#if HDRP_PRESENT
        InstanceSegmentationPass m_InstanceSegmentationPass;
#endif
#if URP_PRESENT
        [NonSerialized]
        InstanceSegmentationUrpPass m_InstanceSegmentationUrpPass;
#endif

        void SetupInstanceSegmentation()
        {
            var camera = GetComponent<Camera>();
            var width = camera.pixelWidth;
            var height = camera.pixelHeight;
            m_InstanceSegmentationTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            m_InstanceSegmentationTexture.name = "InstanceSegmentation";

            m_RenderedObjectInfoGenerator = new RenderedObjectInfoGenerator();

#if HDRP_PRESENT
            var customPassVolume = this.GetComponent<CustomPassVolume>() ?? gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            customPassVolume.isGlobal = true;
            m_SegmentationPass = new InstanceSegmentationPass()
            {
                name = "Instance segmentation pass",
                targetCamera = myCamera,
                targetTexture = m_SegmentationTexture
            };
            m_SegmentationPass.EnsureInit();
            customPassVolume.customPasses.Add(m_SegmentationPass);
#endif
#if URP_PRESENT
            AddScriptableRenderPass(new InstanceSegmentationUrpPass(camera, m_InstanceSegmentationTexture));
#endif

            m_InstanceSegmentationReader = new RenderTextureReader<uint>(m_InstanceSegmentationTexture, camera, (frameCount, data, tex) =>
            {
                InstanceSegmentationImageReadback?.Invoke(frameCount, data, tex);
                if (RenderedObjectInfosCalculated != null)
                {
                    m_RenderedObjectInfoGenerator.Compute(data, tex.width, BoundingBoxOrigin.TopLeft, out var renderedObjectInfos, Allocator.Temp);
                    RenderedObjectInfosCalculated?.Invoke(frameCount, renderedObjectInfos);
                    renderedObjectInfos.Dispose();
                }
            });
        }

        void CleanUpInstanceSegmentation()
        {
            if (m_InstanceSegmentationTexture != null)
                m_InstanceSegmentationTexture.Release();

            m_InstanceSegmentationTexture = null;

            m_InstanceSegmentationReader?.WaitForAllImages();
            m_InstanceSegmentationReader?.Dispose();
            m_InstanceSegmentationReader = null;
        }
    }
}

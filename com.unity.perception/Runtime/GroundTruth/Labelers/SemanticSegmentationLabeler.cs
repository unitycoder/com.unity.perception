using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Simulation;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace UnityEngine.Perception.GroundTruth {
    [AddComponentMenu("Perception/Labelers/SemanticSegmentationLabeler")]
    [RequireComponent(typeof(InstanceSegmentationLabeler))]
    public class SemanticSegmentationLabeler : MonoBehaviour
    {
        const string k_SemanticSegmentationDirectory = "SemanticSegmentation";
        const string k_SegmentationFilePrefix = "segmentation_";

        public string annotationId = "12F94D8D-5425-4DEB-9B21-5E53AD957D66";
        /// <summary>
        /// The SemanticSegmentationLabelConfig to use for determining segmentation pixel values.
        /// </summary>
        public SemanticSegmentationLabelConfig labelConfig;

        [NonSerialized]
        internal RenderTexture semanticSegmentationTexture;

        AnnotationDefinition m_SemanticSegmentationAnnotationDefinition;
        RenderTextureReader<short> m_SemanticSegmentationTextureReader;

#if HDRP_PRESENT
        SemanticSegmentationPass m_SemanticSegmentationPass;
#endif

        Dictionary<int, AsyncAnnotation> m_AsyncAnnotations = new Dictionary<int, AsyncAnnotation>();
        PerceptionCamera m_PerceptionCamera;

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        struct SemanticSegmentationSpec
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public string label_name;
            [UsedImplicitly]
            public int pixel_value;
        }

        struct AsyncSemanticSegmentationWrite
        {
            public short[] dataArray;
            public int width;
            public int height;
            public string path;
        }

        void Start()
        {
            var myCamera = GetComponent<Camera>();
            var width = myCamera.pixelWidth;
            var height = myCamera.pixelHeight;

            if (labelingConfiguration == null)
            {
                Debug.LogError("LabelingConfiguration must be set if producing ground truth data");
                this.enabled = false;
            }

            semanticSegmentationTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            semanticSegmentationTexture.name = "Labeling";
            m_PerceptionCamera = GetComponent<PerceptionCamera>();

#if HDRP_PRESENT
            var customPassVolume = this.GetComponent<CustomPassVolume>() ?? gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            customPassVolume.isGlobal = true;
            m_SemanticSegmentationPass = new SemanticSegmentationPass(myCamera, semanticSegmentationTexture, labelingConfiguration)
            {
                name = "Labeling Pass"
            };
            customPassVolume.customPasses.Add(m_SemanticSegmentationPass);
#endif
#if URP_PRESENT
            m_PerceptionCamera.AddScriptableRenderPass(new SemanticSegmentationUrpPass(myCamera, semanticSegmentationTexture, labelingConfiguration));
#endif

            var specs = labelingConfiguration.LabelEntries.Select((l) => new SemanticSegmentationSpec()
            {
                label_id = l.id,
                label_name = l.label,
                pixel_value = l.value
            }).ToArray();

            m_SemanticSegmentationAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("semantic segmentation", specs, "pixel-wise semantic segmentation label", "PNG");

            m_SemanticSegmentationTextureReader = new RenderTextureReader<short>(semanticSegmentationTexture, myCamera,
                (frameCount, data, tex) => OnSemanticSegmentationImageRead(frameCount, data));

            SimulationManager.SimulationEnding += OnSimulationEnding;
            m_PerceptionCamera.BeginRendering += ReportAsyncAnnotations;
        }

        void OnSemanticSegmentationImageRead(int frameCount, NativeArray<short> data)
        {
            var dxLocalPath = Path.Combine(k_SemanticSegmentationDirectory, k_SegmentationFilePrefix) + frameCount + ".png";
            var path = Path.Combine(Manager.Instance.GetDirectoryFor(k_SemanticSegmentationDirectory), k_SegmentationFilePrefix) + frameCount + ".png";

            if (!m_AsyncAnnotations.TryGetValue(frameCount, out var annotation))
                return;

            annotation.ReportFile(dxLocalPath);

            var asyncRequest = Manager.Instance.CreateRequest<AsyncRequest<AsyncSemanticSegmentationWrite>>();
            asyncRequest.data = new AsyncSemanticSegmentationWrite()
            {
                dataArray = data.ToArray(),
                width = semanticSegmentationTexture.width,
                height = semanticSegmentationTexture.height,
                path = path
            };
            asyncRequest.Start((r) =>
            {
                Profiler.EndSample();
                Profiler.BeginSample("Encode");
                var pngBytes = ImageConversion.EncodeArrayToPNG(r.data.dataArray, GraphicsFormat.R8G8B8A8_UNorm, (uint)r.data.width, (uint)r.data.height);
                Profiler.EndSample();
                Profiler.BeginSample("WritePng");
                File.WriteAllBytes(r.data.path, pngBytes);
                Manager.Instance.ConsumerFileProduced(r.data.path);
                Profiler.EndSample();
                return AsyncRequest.Result.Completed;
            });
        }

        void ReportAsyncAnnotations()
        {
            m_AsyncAnnotations[Time.frameCount] = m_PerceptionCamera.SensorHandle.ReportAnnotationAsync(m_SemanticSegmentationAnnotationDefinition);
        }

        void OnSimulationEnding()
        {
            m_SemanticSegmentationTextureReader?.WaitForAllImages();
            m_SemanticSegmentationTextureReader?.Dispose();
            m_SemanticSegmentationTextureReader = null;
        }

        void OnDisable()
        {
            SimulationManager.SimulationEnding -= OnSimulationEnding;

            m_SemanticSegmentationTextureReader?.Dispose();
            m_SemanticSegmentationTextureReader = null;

            if (semanticSegmentationTexture != null)
                semanticSegmentationTexture.Release();

            semanticSegmentationTexture = null;
        }
    }
}

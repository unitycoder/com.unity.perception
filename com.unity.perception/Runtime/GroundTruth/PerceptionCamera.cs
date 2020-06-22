using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Simulation;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif
#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEngine.Perception.GroundTruth
{
    /// <summary>
    /// Captures ground truth from the associated Camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PerceptionCamera : MonoBehaviour
    {
        const string k_SemanticSegmentationDirectory = "SemanticSegmentation";
        //TODO: Remove the Guid path when we have proper dataset merging in USim/Thea
        internal static string RgbDirectory { get; } = $"RGB{Guid.NewGuid()}";
        static string s_RgbFilePrefix = "rgb_";
        const string k_SegmentationFilePrefix = "segmentation_";

        /// <summary>
        /// A human-readable description of the camera.
        /// </summary>
        public string description;
        /// <summary>
        /// The period in seconds that the Camera should render
        /// </summary>
        public float period = .0166f;
        /// <summary>
        /// The start time in seconds of the first frame in the simulation.
        /// </summary>
        public float startTime;
        /// <summary>
        /// Whether camera output should be captured to disk
        /// </summary>
        public bool captureRgbImages = true;
        /// <summary>
        /// Whether semantic segmentation images should be generated
        /// </summary>
        public bool produceSegmentationImages = true;
        /// <summary>
        /// The LabelingConfiguration to use for segmentation and object count.
        /// </summary>
        public LabelingConfiguration LabelingConfiguration;
        /// <summary>
        /// Event invoked after the camera finishes rendering during a frame.
        /// </summary>
        public event Action BeginRendering;

        [NonSerialized]
        internal RenderTexture semanticSegmentationTexture;

        RenderTextureReader<short> m_SemanticSegmentationTextureReader;
        Dictionary<string, object> m_PersistentSensorData = new Dictionary<string, object>();

#if URP_PRESENT
        internal List<ScriptableRenderPass> passes = new List<ScriptableRenderPass>();
        public void AddScriptableRenderPass(ScriptableRenderPass pass)
        {
            passes.Add(pass);
        }
#endif

        bool m_CapturedLastFrame;

        Ego m_EgoMarker;

        /// <summary>
        /// The <see cref="SensorHandle"/> associated with this camera. Use this to report additional annotations and metrics at runtime.
        /// </summary>
        public SensorHandle SensorHandle { get; private set; }

        struct AsyncSemanticSegmentationWrite
        {
            public short[] dataArray;
            public int width;
            public int height;
            public string path;
        }
        struct AsyncCaptureInfo
        {
            public int FrameCount;
            public AsyncAnnotation SegmentationAsyncAnnotation;
            public AsyncMetric ClassCountAsyncMetric;
            public AsyncMetric RenderedObjectInfoAsyncMetric;
            public AsyncAnnotation BoundingBoxAsyncMetric;
        }

        List<AsyncCaptureInfo> m_AsyncCaptureInfos = new List<AsyncCaptureInfo>();

#if HDRP_PRESENT
        SemanticSegmentationPass m_SemanticSegmentationPass;
#endif
        MetricDefinition m_ObjectCountMetricDefinition;
        AnnotationDefinition m_BoundingBoxAnnotationDefinition;
        AnnotationDefinition m_SemanticSegmentationAnnotationDefinition;
        MetricDefinition m_RenderedObjectInfoMetricDefinition;

        static ProfilerMarker s_WriteFrame = new ProfilerMarker("Write Frame (PerceptionCamera)");
        static ProfilerMarker s_FlipY = new ProfilerMarker("Flip Y (PerceptionCamera)");
        static ProfilerMarker s_EncodeAndSave = new ProfilerMarker("Encode and save (PerceptionCamera)");

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

        /// <summary>
        /// Add a data object which will be added to the dataset with each capture. Overrides existing sensor data associated with the given key.
        /// </summary>
        /// <param name="key">The key to associate with the data.</param>
        /// <param name="data">An object containing the data. Will be serialized into json.</param>
        public void SetPersistentSensorData(string key, object data)
        {
            m_PersistentSensorData[key] = data;
        }

        /// <summary>
        /// Removes a persistent sensor data object.
        /// </summary>
        /// <param name="key">The key of the object to remove.</param>
        /// <returns>True if a data object was removed. False if it was not set.</returns>
        public bool RemovePersistentSensorData(string key)
        {
            return m_PersistentSensorData.Remove(key);
        }

        // Start is called before the first frame update
        void Awake()
        {
            m_EgoMarker = this.GetComponentInParent<Ego>();
            var ego = m_EgoMarker == null ? SimulationManager.RegisterEgo("") : m_EgoMarker.EgoHandle;
            SensorHandle = SimulationManager.RegisterSensor(ego, "camera", description, period, startTime);

            var myCamera = GetComponent<Camera>();
            var width = myCamera.pixelWidth;
            var height = myCamera.pixelHeight;

            if (produceSegmentationImages && LabelingConfiguration == null)
            {
                Debug.LogError("LabelingConfiguration must be set if producing ground truth data");
                produceSegmentationImages = false;
            }

            semanticSegmentationTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            semanticSegmentationTexture.name = "Labeling";

#if HDRP_PRESENT
            var customPassVolume = this.GetComponent<CustomPassVolume>() ?? gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            customPassVolume.isGlobal = true;
            m_SemanticSegmentationPass = new SemanticSegmentationPass(myCamera, semanticSegmentationTexture, LabelingConfiguration)
            {
                name = "Labeling Pass"
            };

            SetupPasses(customPassVolume);
#endif
#if URP_PRESENT
            AddScriptableRenderPass(new SemanticSegmentationUrpPass(myCamera, semanticSegmentationTexture, LabelingConfiguration));
#endif

            if (produceSegmentationImages)
            {
                var specs = LabelingConfiguration.LabelEntries.Select((l) => new SemanticSegmentationSpec()
                {
                    label_id = l.id,
                    label_name = l.label,
                    pixel_value = l.value
                }).ToArray();

                m_SemanticSegmentationAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("semantic segmentation", specs, "pixel-wise semantic segmentation label", "PNG");

                m_SemanticSegmentationTextureReader = new RenderTextureReader<short>(semanticSegmentationTexture, myCamera,
                    (frameCount, data, tex) => OnSemanticSegmentationImageRead(frameCount, data));
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            SimulationManager.SimulationEnding += OnSimulationEnding;
        }

#if HDRP_PRESENT
        void SetupPasses(CustomPassVolume customPassVolume)
        {
            customPassVolume.customPasses.Remove(m_SemanticSegmentationPass);

            if (produceSegmentationImages)
                customPassVolume.customPasses.Add(m_SemanticSegmentationPass);
        }
#endif

        (int index, AsyncCaptureInfo asyncCaptureInfo) FindAsyncCaptureInfo(int frameCount)
        {
            for (var i = 0; i < m_AsyncCaptureInfos.Count; i++)
            {
                var captureInfo = m_AsyncCaptureInfos[i];
                if (captureInfo.FrameCount == frameCount)
                {
                    return (i, captureInfo);
                }
            }

            return (-1, default);
        }

        // Update is called once per frame
        void Update()
        {
            if (!SensorHandle.IsValid)
                return;

            var cam = GetComponent<Camera>();
            cam.enabled = SensorHandle.ShouldCaptureThisFrame;

            m_AsyncCaptureInfos.RemoveSwapBack(i =>
                !i.SegmentationAsyncAnnotation.IsPending &&
                !i.BoundingBoxAsyncMetric.IsPending &&
                !i.RenderedObjectInfoAsyncMetric.IsPending &&
                !i.ClassCountAsyncMetric.IsPending);
        }

        void ReportAsyncAnnotations()
        {
            if (produceSegmentationImages)
            {
                var captureInfo = new AsyncCaptureInfo()
                {
                    FrameCount = Time.frameCount
                };
                if (produceSegmentationImages)
                    captureInfo.SegmentationAsyncAnnotation = SensorHandle.ReportAnnotationAsync(m_SemanticSegmentationAnnotationDefinition);

                m_AsyncCaptureInfos.Add(captureInfo);
            }
        }

        void CaptureRgbData(Camera cam)
        {
            Profiler.BeginSample("CaptureDataFromLastFrame");
            if (!captureRgbImages)
                return;

            var captureFilename = Path.Combine(Manager.Instance.GetDirectoryFor(RgbDirectory), $"{s_RgbFilePrefix}{Time.frameCount}.png");
            var dxRootPath = Path.Combine(RgbDirectory, $"{s_RgbFilePrefix}{Time.frameCount}.png");
            SensorHandle.ReportCapture(dxRootPath, SensorSpatialData.FromGameObjects(m_EgoMarker == null ? null : m_EgoMarker.gameObject, gameObject), m_PersistentSensorData.Select(kvp => (kvp.Key, kvp.Value)).ToArray());

            Func<AsyncRequest<CaptureCamera.CaptureState>, AsyncRequest.Result> colorFunctor = null;
            var width = cam.pixelWidth;
            var height = cam.pixelHeight;
            var flipY = ShouldFlipY(cam);

            colorFunctor = r =>
            {
                using (s_WriteFrame.Auto())
                {
                    var dataColorBuffer = (byte[])r.data.colorBuffer;
                    if (flipY)
                        FlipImageY(dataColorBuffer, height);

                    byte[] encodedData;
                    using (s_EncodeAndSave.Auto())
                    {
                        encodedData = ImageConversion.EncodeArrayToPNG(dataColorBuffer, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height);
                    }

                    return !FileProducer.Write(captureFilename, encodedData) ? AsyncRequest.Result.Error : AsyncRequest.Result.Completed;
                }
            };

            CaptureCamera.Capture(cam, colorFunctor);

            Profiler.EndSample();
        }

        // ReSharper disable once ParameterHidesMember
        bool ShouldFlipY(Camera camera)
        {
#if HDRP_PRESENT
            var hdAdditionalCameraData = GetComponent<HDAdditionalCameraData>();

            //Based on logic in HDRenderPipeline.PrepareFinalBlitParameters
            return camera.targetTexture != null || hdAdditionalCameraData.flipYMode == HDAdditionalCameraData.FlipYMode.ForceFlipY || camera.cameraType == CameraType.Game;
#elif URP_PRESENT
            return (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) &&
                (camera.targetTexture != null || camera.cameraType == CameraType.Game);
#else
            return false;
#endif
        }

        static unsafe void FlipImageY(byte[] dataColorBuffer, int height)
        {
            using (s_FlipY.Auto())
            {
                var stride = dataColorBuffer.Length / height;
                var buffer = new NativeArray<byte>(stride, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                fixed(byte* colorBufferPtr = &dataColorBuffer[0])
                {
                    var unsafePtr = (byte*)buffer.GetUnsafePtr();
                    for (var row = 0; row < height / 2; row++)
                    {
                        var nearRowStartPtr = colorBufferPtr + stride * row;
                        var oppositeRowStartPtr = colorBufferPtr + stride * (height - row - 1);
                        UnsafeUtility.MemCpy(unsafePtr, oppositeRowStartPtr, stride);
                        UnsafeUtility.MemCpy(oppositeRowStartPtr, nearRowStartPtr, stride);
                        UnsafeUtility.MemCpy(nearRowStartPtr, unsafePtr, stride);
                    }
                }
                buffer.Dispose();
            }
        }

        void OnSimulationEnding()
        {
            m_SemanticSegmentationTextureReader?.WaitForAllImages();
            m_SemanticSegmentationTextureReader?.Dispose();
            m_SemanticSegmentationTextureReader = null;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
        {
            if (cam != GetComponent<Camera>())
                return;
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                return;
#endif
            BeginRendering?.Invoke();
            ReportAsyncAnnotations();
            CaptureRgbData(cam);
        }

        void OnDisable()
        {
            SimulationManager.SimulationEnding -= OnSimulationEnding;

            OnSimulationEnding();

            m_SemanticSegmentationTextureReader?.Dispose();
            m_SemanticSegmentationTextureReader = null;

            if (semanticSegmentationTexture != null)
                semanticSegmentationTexture.Release();

            if (SensorHandle.IsValid)
                SensorHandle.Dispose();

            SensorHandle = default;

            semanticSegmentationTexture = null;
        }

        void OnSemanticSegmentationImageRead(int frameCount, NativeArray<short> data)
        {
            var findResult = FindAsyncCaptureInfo(frameCount);
            var asyncCaptureInfo = findResult.asyncCaptureInfo;

            var dxLocalPath = Path.Combine(k_SemanticSegmentationDirectory, k_SegmentationFilePrefix) + frameCount + ".png";
            var path = Path.Combine(Manager.Instance.GetDirectoryFor(k_SemanticSegmentationDirectory), k_SegmentationFilePrefix) + frameCount + ".png";
            var annotation = asyncCaptureInfo.SegmentationAsyncAnnotation;
            if (!annotation.IsValid)
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
    }
}

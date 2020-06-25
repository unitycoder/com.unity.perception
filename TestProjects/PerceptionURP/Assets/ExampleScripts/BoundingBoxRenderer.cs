using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Simulation;

[RequireComponent(typeof(Camera))]
public class BoundingBoxRenderer : MonoBehaviour
{
    public GameObject light;
    public GameObject target;

    private PerceptionCamera perception;

    private void Awake() 
    {
        perception = GetComponent<PerceptionCamera>();
        CaptureOptions.useAsyncReadbackIfSupported = false;
    }        
}
#if false
    private RenderTexture renderTexture = null;
    private Texture2D cpuTexture = null;

    private AnnotationDefinition bbDef;

    private RenderedObjectInfoGenerator renderedObjectInfoGenerator;

    private RenderTextureReader<uint> segmentationReader;

    private void Awake() 
    {
        var cam = GetComponent<Camera>();
        var width = cam.pixelWidth;
        var height = cam.pixelHeight;

        renderTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
        renderTexture.name = "segmentation";

        cpuTexture = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, TextureCreationFlags.None);

        bbDef = SimulationManager.RegisterAnnotationDefinition(
            "Target bounding box",
            "The position of the target in the camera's local space",
            id: Guid.Parse("C0B4A22C-0420-4D9F-BAFC-954B8F7B35A7"));
#if false
        renderedObjectInfoGenerator = new RenderedObjectInfoGenerator(labelingConfiguration);    
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<GroundTruthLabelSetupSystem>().Activate(renderedObjectInfoGenerator);

        segmentationReader = new RenderTextureReader<uint>(segmentationTexture, cam, (frameCount, data, tex) =>
        {
            if (segmentationImageReceived != null)
                segmentationImageReceived(frameCount, data);

            m_RenderedObjectInfoGenerator.Compute(data, tex.width, boundingBoxOrigin, out var renderedObjectInfos, out var classCounts, Allocator.Temp);

            using (s_RenderedObjectInfosCalculatedEvent.Auto())
                renderedObjectInfosCalculated?.Invoke(frameCount, renderedObjectInfos);

            if (produceObjectCountAnnotations)
                OnObjectCountsReceived(classCounts, LabelingConfiguration.LabelEntries, frameCount);

            if (produceBoundingBoxAnnotations)
                ProduceBoundingBoxesAnnotation(renderedObjectInfos, LabelingConfiguration.LabelEntries, frameCount);

            if (produceRenderedObjectInfoMetric)
                ProduceRenderedObjectInfoMetric(renderedObjectInfos, frameCount);
        });
#endif
    }
    void Update()
    {
        RenderTexture.active = renderTexture;
        cpuTexture.ReadPixels(new Rect(Vector2.zero, new Vector2(renderTexture.width, renderTexture.height)), 0, 0);
        RenderTexture.active = null;
        var data = cpuTexture.GetRawTextureData<uint>();
        ImageReadCallback(Time.frameCount, data, renderTexture);

        Vector3 targetPos = transform.worldToLocalMatrix * target.transform.position;

        var sensorHandle = GetComponent<PerceptionCamera>().SensorHandle;
        var anno = sensorHandle.ReportAnnotationValues(bbDef, new[] { targetPos });
        string jsonValues = string.Empty;
        MetricDefinition metricDef = SimulationManager.RegisterMetricDefinition()
        anno.ReportMetric(bbDef, jsonValues);

 //       if (sensorHandle.ShouldCaptureThisFrame)
 //       {
 //           var anno = sensorHandle.ReportAnnotationValues(bbDef, new[] { targetPos });
 //       }
    }
    
    void ProduceBoundingBoxesAnnotation(NativeArray<RenderedObjectInfo> renderedObjectInfos, List<LabelEntry> labelingConfigurations, int frameCount)
    {


        RenderTextureReader<short>();

            using (s_BoundingBoxCallback.Auto())
            {
                var findResult = FindAsyncCaptureInfo(frameCount);
                if (findResult.index == -1)
                    return;
                var asyncCaptureInfo = findResult.asyncCaptureInfo;
                var boundingBoxAsyncAnnotation = asyncCaptureInfo.BoundingBoxAsyncMetric;
                if (!boundingBoxAsyncAnnotation.IsValid)
                    return;

                if (m_BoundingBoxValues == null || m_BoundingBoxValues.Length != renderedObjectInfos.Length)
                    m_BoundingBoxValues = new BoundingBoxValue[renderedObjectInfos.Length];

                for (var i = 0; i < renderedObjectInfos.Length; i++)
                {
                    var objectInfo = renderedObjectInfos[i];
                    if (!TryGetLabelEntryFromInstanceId(objectInfo.instanceId, out var labelEntry))
                        continue;

                    m_BoundingBoxValues[i] = new BoundingBoxValue
                    {
                        label_id = labelEntry.id,
                        label_name = labelEntry.label,
                        instance_id = objectInfo.instanceId,
                        x = objectInfo.boundingBox.x,
                        y = objectInfo.boundingBox.y,
                        width = objectInfo.boundingBox.width,
                        height = objectInfo.boundingBox.height,
                    };
                }

                boundingBoxAsyncAnnotation.ReportValues(m_BoundingBoxValues);
            }
    }

    void ImageReadCallback(int frameCount, NativeArray<uint> data, RenderTexture renderTexture)
    {
        Debug.Log("Current frame: " + Time.frameCount + ", working frame: " + frameCount);

        // First thing lets check that we are the correct frame...
        if (frameCount == Time.frameCount)
        {
            Debug.Log("We are operating on the current frame");
        }
        else
        {
            Debug.Log("We are on a different frame");
        }
    }
}
#endif
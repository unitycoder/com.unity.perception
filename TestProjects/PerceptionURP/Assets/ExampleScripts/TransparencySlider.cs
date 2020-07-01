using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.UI;

public class TransparencySlider : MonoBehaviour
{
    public PerceptionCamera camera;
    private Slider slider;

    private bool dontEcho = false;

    // Start is called before the first frame update
    void Start()
    {
        slider = GetComponent<Slider>();
        dontEcho = true;
        slider.value = camera.GetVisualizeSegmentationTextureTransparency();
        dontEcho = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnValueChanged(float updated)
    {
        if (!dontEcho) camera.SetVisualizeSegmentationTextureTransparency(slider.value);
    }
}

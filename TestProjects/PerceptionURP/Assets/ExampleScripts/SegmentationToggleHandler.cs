using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;

public class SegmentationToggleHandler : MonoBehaviour
{
    public PerceptionCamera perceptionCamera;

    // Start is called before the first frame update
    void Start()
    {
        // TODO set up the default toggle state based on 
        // the perception cameras default values
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnToggled(bool enabled)
    {
        perceptionCamera.SetEnableSegmentationVisualization(enabled);
    }
}

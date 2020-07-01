using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;

public class BoxToggleHandler : MonoBehaviour
{
    public PerceptionCamera perceptionCamera;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnToggle(bool enabled)
    {
        perceptionCamera.SetBoundsVisualizationEnabled(enabled);
    }
}

using UnityEngine;
using Data;

public static class EOIRCameraProvider
{
    // Returns the Unity camera rendering the EO/IR video feed
    public static Camera GetCamera()
    {
        // Replace with actual logic to retrieve the EO/IR camera
        GameObject eoCameraObject = GameObject.Find("EOIRCamera");
        if (eoCameraObject != null)
        {
            return eoCameraObject.GetComponent<Camera>();
        }

        Debug.LogWarning("EOIRCamera not found. Using Camera.main as fallback.");
        return Camera.main;
    }

    // Returns the transform used as the anchor for screen overlays
    public static Transform GetAnchorTransform()
    {
        GameObject anchorObject = GameObject.Find("EOIRScreenAnchor");
        if (anchorObject != null)
        {
            return anchorObject.transform;
        }

        Debug.LogWarning("EOIRScreenAnchor not found. Returning null.");
        return null;
    }
}
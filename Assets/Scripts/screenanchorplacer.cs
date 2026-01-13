using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

public class ScreenAnchorPlacer : MonoBehaviour
{
    public ARPlaneManager planeManager;
    public ARInitializationManager arManager;
    public GameObject screenAnchorPrefab;
    private bool anchorPlaced = false;


    void Start()
    {
        StartCoroutine(WaitForARAndPlaceAnchor());
    }

    IEnumerator WaitForARAndPlaceAnchor()
    {

        #if UNITY_EDITOR
            Debug.Log("Editor mode: skipping ARKit wait");
        #else
            while (ARSession.state != ARSessionState.SessionTracking)
            {
                Debug.Log("Waiting for ARKit to be ready...");
                yield return null;
            }
        #endif

        Debug.Log("ARKit is ready. Looking for planes...");


        while (!anchorPlaced)
        {
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp || 
                plane.alignment == PlaneAlignment.Vertical)
                {
                    Pose pose = new Pose(plane.center, Quaternion.identity);
                    GameObject anchor = Instantiate(screenAnchorPrefab, pose.position, pose.rotation);
                    anchorPlaced = true;
                    arManager.OnScreenAnchorDetected(anchor);
                    Debug.Log("ScreenAnchhor placed at plane position.");
                    break;
                }
            }

            yield return null;
        }
    }
}
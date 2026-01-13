using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARKitStatusDebugger : MonoBehaviour
{
    void Update()
    {
        #if UNITY_EDITOR
            Debug.Log("Editor mode: ARKit not available. Simulating ARSessionState.SessionTracking.");
        #else
            Debug.Log("AR Session State: " + ARSession.state);
            switch (ARSession.state)
            {
                case ARSessionState.None:
                    Debug.LogWarning("ARSessionState.None – AR not initialized.");
                    break;
                case ARSessionState.Unsupported:
                    Debug.LogError("ARSessionState.Unsupported – ARKit is not supported on this device.");
                    break;
                case ARSessionState.CheckingAvailability:
                    Debug.Log("Checking ARKit availability...");
                    break;
                case ARSessionState.NeedsInstall:
                    Debug.LogWarning("ARKit needs to be installed.");
                    break;
                case ARSessionState.Installing:
                    Debug.Log("Installing ARKit...");
                    break;
                case ARSessionState.Ready:
                    Debug.Log("ARKit is ready.");
                    break;
                case ARSessionState.SessionInitializing:
                    Debug.Log("ARKit session is initializing.");
                    break;
                case ARSessionState.SessionTracking:
                    Debug.Log("ARKit is actively tracking.");
                    break;
            }  
        #endif    
    }
}

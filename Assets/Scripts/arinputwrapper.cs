using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARInputManagerWrapper : MonoBehaviour
{
    void Awake()
    {
#if UNITY_EDITOR
    ARInputManager inputManager = GetComponent<ARInputManager>();
    if (inputManager != null)
    {
        Debug.Log("Editor mode: disabling ARInputManager.");
        inputManager.enabled = false;
    }
#endif    
    }
}
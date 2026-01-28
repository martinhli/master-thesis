using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ARInitializationManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARSession arSession;
    public Camera arCamera;
    public AISOverlay overlaySystem;
    public dataparser parser;

    [Header("Dummy Data")]
    public TextAsset eoJsonFile;
    public TextAsset aisJsonFile;

    [Header("EO/IR Components")]
    public Camera eoCamera;
    public RectTransform screenTransform;

    [Header("Video Stream Components")]
    public FFmpegReader ffmpegreader;

    [Header("UI")]
    public Canvas uiCanvas;                 // assign in Inspector or auto-find
    public GameObject overlayPanelPrefab;   // Panel prefab with RectTransform

    void Awake()
    {
        InitializeEOIRSystem();   // run BEFORE other Start() methods
    }

    void Start()
    {
        // If you later want AR flow, you can still use this:
        // StartCoroutine(InitializeARFlow());
    }

    IEnumerator InitializeARFlow()
    {
        while (ARSession.state != ARSessionState.SessionTracking)
        {
            Debug.Log("ARSession state: " + ARSession.state);
            if (ARSession.state == ARSessionState.Unsupported)
            {
                Debug.LogError("ARKit is not supported on this device");
                yield break;
            }
            yield return null;
        }

        Debug.Log("ARKit is ready.");

        while (arCamera == null)
        {
            arCamera = Camera.main;
            yield return null;
        }

        overlaySystem.eoCamera = arCamera;
        Debug.Log("AR Camera assigned.");
    }

    void InitializeEOIRSystem()
    {
        if (overlaySystem == null)
        {
            overlaySystem = FindObjectOfType<AISOverlay>();
            if (overlaySystem == null)
            {
                Debug.LogError("ARInitializationManager: No AISOverlay found in scene.");
                return;
            }
        }

        // EO camera
        eoCamera = EOIRCameraProvider.GetCamera();
        overlaySystem.eoCamera = eoCamera;

        // --- UI panel instantiation for screenTransform ---
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                // Create a Canvas at runtime if none exists
                GameObject canvasGO = new GameObject("AutoCanvas");
                canvasGO.layer = LayerMask.NameToLayer("UI");
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();

                // Ensure an EventSystem exists for UI interactions
                if (FindObjectOfType<EventSystem>() == null)
                {
                    GameObject es = new GameObject("EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                }
                Debug.Log("ARInitializationManager: Created runtime Canvas (AutoCanvas).");
            }
        }

        overlaySystem.screenTransform = uiCanvas != null ? uiCanvas.GetComponent<RectTransform>() : null;
        Debug.Log("ARInit: screenTransform assigned from UI Canvas.");

        
        if (uiCanvas != null)
        {
            GameObject panelInstance = Instantiate(overlayPanelPrefab, uiCanvas.transform);
            screenTransform = panelInstance.GetComponent<RectTransform>();

            if (screenTransform == null)
            {
                Debug.LogError("ARInitializationManager: overlayPanelPrefab instance has no RectTransform.");
            }
            else
            {
                overlaySystem.screenTransform = screenTransform;
                Debug.Log("ARInitializationManager: OverlayPanel instantiated and assigned to AISOverlay.screenTransform.");
            }
        }
        else
        {
            Debug.LogWarning("ARInitializationManager: overlayPanelPrefab is not assigned. Using Canvas RectTransform as screenTransform.");
        }

        // Wire overlay into parser
        if (parser == null)
        {
            parser = FindObjectOfType<dataparser>();
        }
        if (parser != null)
        {
            parser.overlaySystem = overlaySystem;
        }
        else
        {
            Debug.LogError("ARInitializationManager: dataparser not assigned or found.");
        }

        // Wire parser and AIS data into FFmpeg reader
        if (ffmpegreader == null)
        {
            ffmpegreader = FindObjectOfType<FFmpegReader>();
        }
        if (ffmpegreader != null)
        {
            ffmpegreader.parser = parser;
        }
        else
        {
            Debug.LogError("ARInitializationManager: FFmpegReader not assigned or found.");
        }

        // Load dummy metadata
        if (aisJsonFile != null && parser != null)
        {
            string aisString = aisJsonFile.text;
            AISData aisData = parser.parseAISData(aisString);
            ffmpegreader.aisData = aisData;
        }

        if (eoJsonFile != null && parser != null)
        {
            string eoString = eoJsonFile.text;
            EOIRMetadata eoData = parser.parseEOIRMetadata(eoString);
        }
    }

    public void OnScreenAnchorDetected(GameObject anchor)
    {
        Debug.Log("Screen anchor detected (AR object), but UI overlay uses a fixed RectTransform.");
        var ships = new List<Data.Ship>
        {
            new Data.Ship { name = "HUGIN", imo = "9074729", speed = 12.3f, course = 315f }
        };
        Debug.Log($"Overlay placed for {ships.Count} ships (debug log only).");
    }
}

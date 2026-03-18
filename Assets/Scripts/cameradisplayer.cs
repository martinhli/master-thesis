using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class cameradisplayer : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The EOIR camera to display the feed from")]
    public Camera eoirCamera;

    [Header("Display Settings")]
    [Tooltip("The renderer of the display surface (e.g., a plane or quad)")]
    public RawImage displayImage;

    [Tooltip("Resolution of the camera feed")]
    public Vector2Int cameraResolution = new Vector2Int(1024, 768);

    [Header("UI Elements")]
    [Tooltip("Panel containing the crosshair UI elements")]
    public GameObject crosshairPanel;
    public GameObject reticlePanel;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI zoomText;

    public TextMeshProUGUI rangeText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI timeText;

    [Header("Display Options")]
    [Tooltip("Toggle between camera mode (showing EOIR feed) and map mode (showing a map or other UI)")]
    public bool showOverlay = true; 
    public string displayMode = "Camera"; // Options: "Camera", "Map"

    private RenderTexture renderTexture;

    void Start()
    {
        // Need to setup the camera feed and render texture
        SetUpCameraFeed();
        SetupUI();
    }

    void Update()
    {
        // Need a function to set up telemetry data and update the UI elements
        UpdateTelemetry();
    }

    void SetUpCameraFeed()
    {
        // If the EOIR camera is not assigned, try to find it in the scene
        if (eoirCamera == null)
        {
            Debug.LogWarning("[CameraDisplayer] EOIR camera is not assigned. Attempting to find camera tagged 'EOIRCamera'...");
            return;
        }

        // If the EOIR cameras screen renderer is not assigned, try to find it in the scene
        if (screenRenderer == null)
        {
            Debug.LogWarning("[CameraDisplayer] Screen renderer is not assigned. Attempting to find renderer tagged 'CameraScreen'...");
            return;
        }

        // Create a render texture for the camera feed
        renderTexture = new RenderTexture(cameraResolution.x, cameraResolution.y, 24);
        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.Create();

        // Assign the render texture to the EOIR camera
        eoirCamera.targetTexture = renderTexture;
        
        // Assign the render texture to the display image
        if (displayImage != null)
        {
            displayImage.texture = renderTexture;
        }

        Debug.Log("[CameraDisplayer] Camera feed setup complete. Resolution: " + cameraResolution.x + "x" + cameraResolution.y); 
    }

    void SetupUI()
    {
        if (crosshairPanel != null)
        {
            crosshairPanel.SetActive(showOverlay);
        }
        if (reticlePanel != null)
        {
            reticlePanel.SetActive(showOverlay);
        }
    }

    void UpdateTelemetry()
    {
        if (eoirCamera == null) return;

        // Update mode
        if (modeText != null)
        {
            modeText.text = $"MODE: {displayMode.ToUpper()}"; 
        }
        if (zoomText != null)
        {
            zoomText.text = $"ZOOM: {eoirCamera.fieldOfView:F1}"; 
        }
        if (rangeText != null)
        {
            rangeText.text = $"RANGE: AUTO"; 
        }
        if (distanceText != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(eoirCamera.transform.position, eoirCamera.transform.forward, out hit))
            {
                float distancekm = hit.distance / 1000f;
                distanceText.text = $"DIST: {distancekm:F2}km";
            }
            else
            {
                distanceText.text = $"DIST: N/A";
            }
        }
        if (timeText != null)
        {
            timeText.text = $"TIME: {System.DateTime.Now:HH:mm:ss}";
        }
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            if (eoirCamera != null)
            {
                eoirCamera.targetTexture = null;
            }
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    // Update zoom level based on user input (for testing purposes)

    public void SetZoomLevel(float zoomLevel)
    {
        if (eoirCamera != null)
        {
            eoirCamera.fieldOfView = Mathf.Clamp(zoomLevel, 10f, 60f);
        }
    }
    public void ZoomIn()
    {
        if (eoirCamera != null)
        {
            eoirCamera.fieldOfView = MathF.Max(10f, eoirCamera.fieldOfView - 5f);
        }
    }

    public void ZoomOut()
    {
        if (eoirCamera != null)
        {
            eoirCamera.fieldOfView = MathF.Min(60f, eoirCamera.fieldOfView + 5f);
        }
    }

    public void ToggleOverlay()
    {
        showOverlay = !showOverlay;
        if (crosshairPanel != null)
        {
            crosshairPanel.SetActive(showOverlay);
        }
        if (reticlePanel != null)
        {
            reticlePanel.SetActive(showOverlay);
        }
    }

    public void SetMode(string mode)
    {
        displayMode = mode;
    }

    


}
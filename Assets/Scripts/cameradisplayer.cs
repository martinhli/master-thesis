using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class cameradisplayer : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera eoirCamera;
    
    [Header("Display Settings")]
    public RawImage displayImage;
    public Vector2Int resolution = new Vector2Int(1024, 768);
    
    [Header("UI Elements")]
    public GameObject crosshairPanel;
    public GameObject reticlePanel;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI zoomText;
    public TextMeshProUGUI rangeText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI timeText;
    
    [Header("Settings")]
    public bool showOverlay = true;
    public string cameraMode = "EO/IR";
    
    private RenderTexture renderTexture;
    
    void Start()
    {
        SetupCameraFeed();
        SetupUI();
    }
    
    void SetupCameraFeed()
    {
        if (eoirCamera == null)
        {
            Debug.LogError("[CameraFeedDisplay_Canvas] No camera assigned!");
            return;
        }
        
        // Create render texture
        renderTexture = new RenderTexture(resolution.x, resolution.y, 24);
        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.Create();
        
        // Assign to camera
        eoirCamera.targetTexture = renderTexture;
        
        // Assign to UI display
        if (displayImage != null)
        {
            displayImage.texture = renderTexture;
        }
        
        Debug.Log("[CameraFeedDisplay_Canvas] Setup complete");
    }
    
    void SetupUI()
    {
        if (crosshairPanel != null)
            crosshairPanel.SetActive(showOverlay);
            
        if (reticlePanel != null)
            reticlePanel.SetActive(showOverlay);
    }
    
    void Update()
    {
        UpdateTelemetry();
    }
    
    void UpdateTelemetry()
    {
        if (eoirCamera == null) return;
        
        // Update mode
        if (modeText != null)
            modeText.text = $"MODE: {cameraMode}";
        
        // Update zoom
        if (zoomText != null)
            zoomText.text = $"ZOOM: {eoirCamera.fieldOfView:F1}°";
        
        // Update range mode
        if (rangeText != null)
            rangeText.text = "RNG: AUTO";
        
        // Update distance
        if (distanceText != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(eoirCamera.transform.position, eoirCamera.transform.forward, out hit, 50000f))
            {
                float distanceKm = hit.distance / 1000f;
                distanceText.text = $"DIST: {distanceKm:F2} km";
            }
            else
            {
                distanceText.text = "DIST: --";
            }
        }
        
        // Update time
        if (timeText != null)
            timeText.text = $"[REC] {System.DateTime.Now:HH:mm:ss}";
    }
    
    void OnDestroy()
    {
        if (renderTexture != null)
        {
            if (eoirCamera != null)
                eoirCamera.targetTexture = null;
            
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
    
    // Public control methods
    public void SetZoom(float fov)
    {
        if (eoirCamera != null)
            eoirCamera.fieldOfView = Mathf.Clamp(fov, 10f, 60f);
    }
    
    public void ZoomIn()
    {
        if (eoirCamera != null)
            eoirCamera.fieldOfView = Mathf.Max(10f, eoirCamera.fieldOfView - 5f);
    }
    
    public void ZoomOut()
    {
        if (eoirCamera != null)
            eoirCamera.fieldOfView = Mathf.Min(60f, eoirCamera.fieldOfView + 5f);
    }
    
    public void ToggleOverlay()
    {
        showOverlay = !showOverlay;
        
        if (crosshairPanel != null)
            crosshairPanel.SetActive(showOverlay);
            
        if (reticlePanel != null)
            reticlePanel.SetActive(showOverlay);
    }
    
    public void SetMode(string mode)
    {
        cameraMode = mode;
    }
}
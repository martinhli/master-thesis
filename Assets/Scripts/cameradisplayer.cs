using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class cameradisplayer : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera eoirCamera;
    public Transform aircraftTransform;
    
    [Header("Display Settings")]
    public RawImage displayImage;
    public Vector2Int resolution = new Vector2Int(1024, 768);
    
    [Header("UI Elements")]
    public GameObject crosshairPanel;
    public GameObject reticlePanel;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI directionText;
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
        
        // Update camera mode
        if (modeText != null)
            modeText.text = $"MODE: {cameraMode}";
        
        // Update direction mode
        if (directionText != null)
        {
            Transform referenceTransform = ResolveAircraftTransform();
            if (referenceTransform != null)
            {
                Vector3 referenceFlat = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up);
                Vector3 cameraFlat = Vector3.ProjectOnPlane(eoirCamera.transform.forward, Vector3.up);
                if (referenceFlat.sqrMagnitude > 0.0001f && cameraFlat.sqrMagnitude > 0.0001f)
                {
                    float bearing = Vector3.SignedAngle(referenceFlat, cameraFlat, Vector3.up);
                    if (bearing < 0f)
                    {
                        // Convert to relative bearing from nose in 0-360 format
                        bearing = (bearing + 360f) % 360f;
                    }

                    directionText.text = $"BRG: {bearing:000}° from nose";
                }
                else
                {
                    directionText.text = "BRG: ---";
                }
            }
            else
            {
                directionText.text = "BRG: ---";
            }
        }
        
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

    private static string GetCompassNotation(float bearingDegrees)
    {
        // 8-point compass notation for quick operator readability.
        string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = Mathf.RoundToInt(Mathf.Repeat(bearingDegrees, 360f) / 45f) % points.Length;
        return points[index];
    }

    private Transform ResolveAircraftTransform()
    {
        if (aircraftTransform != null)
            return aircraftTransform;

        if (eoirCamera != null)
            return eoirCamera.transform.parent;

        return null;
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
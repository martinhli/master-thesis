using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Data;

using TMPro;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
public class EOIRCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The EOIR camera that renders the ship scene")]
    public Camera eoirCamera;
    public Transform cameraMount;

    [Header("Camera Control Parameters")]
    [Tooltip("How far can the camera pan left/right (degrees)")]
    public float maxPan = 180f;

    [Tooltip("How far can the camera tilt up (degrees)")]
    public float maxTilt = 20f;

    [Tooltip("How far can the camera tilt down (degrees)")]
    public float minTilt = -90f;

    [Tooltip("Zoom speed (units per second)")]
    public float zoomSpeed = 20f;

    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 30f;

    [Tooltip ("Minimum FOV (max zoom in)")]
    public float minFOV = 5f;

    [Tooltip("Maximum FOV (max zoom out)")]
    public float maxFOV = 60f;

    [Header("Current Camera State")]
    public float currentPan = 0f;
    public float currentTilt = 15f;
    public float currentZoom = 45f;
    private Vector3 targetRotation;

    [Header("Camera Capture Feedback")]
    public TextMeshProUGUI statusText;
    public Image flashEffect;

    [Header("Detection Parameters")]

    [Tooltip("YOLO detector used to classify captured EO/IR images")]
    public yolodetector yoloDetector;

    [Tooltip("Enable YOLO-based confirmation/rejection for capture events")]
    public bool useYOLODetection = true;

    [Tooltip("Require a valid center hit when YOLO reports a vessel")]
    public bool requireRaycastHitForYOLOConfirmation = true;

    [Tooltip("Capture resolution width for YOLO input")]
    public int captureWidth = 640;

    [Tooltip("Capture resolution height for YOLO input")]
    public int captureHeight = 640;

    [Tooltip("Optional expected unknown contact for the current scenario")]
    public SimulatedShip expectedUnknownContact;

    [Tooltip("When enabled, only the expected unknown contact counts as confirmed")]
    public bool requireExpectedContactMatch = false;

    [Tooltip("Raycast detector")]
    public bool useRaycastDetection = true;

    [Tooltip("Detection range in meters")]
    public float detectionRange = 15000f;

    [Tooltip("Spherecast target object")]
    public GameObject sphereCastTarget;

    [Header("Input Settings")]

    [Tooltip("Input key to capture camera control input")]
    public KeyCode captureInputKey = KeyCode.Space;

    [Tooltip("VR controller input button to capture camera control input")]
    public bool useVRControllerInput = false;

    // Event to notify when a ship is detected
    public event System.Action<SimulatedShip> OnShipDetected;
    public event System.Action OnNoShipDetected;

    void Start()
    {
        if (eoirCamera == null)
        {
            eoirCamera = GetComponent<Camera>();
            Debug.LogError("[EO/IR] No camera assigned!");
            return;
        }
        if (cameraMount == null)
        {
            cameraMount = transform.parent;
        }

        // Initialize camera orientation
        eoirCamera.fieldOfView = currentZoom;

        // Need a function to set the initial rotation of the camera based on currentPan and currentTilt
        ApplyCameraRotation();

        UpdateStatusText("[EO/IR] EO/IR Camera Ready");
    }

    void Update()
    {
        HandleCameraControl();
        UpdateCamera();
        HandleCapture();
    }

    /// <summary>
    /// Camera Handling Functions
    /// </summary>
    
    void UpdateCamera()
    {
        // Apply zoom
        eoirCamera.fieldOfView = Mathf.Lerp(eoirCamera.fieldOfView, currentZoom, Time.deltaTime * zoomSpeed);
        // Apply rotation
        ApplyCameraRotation();
    }

    void ApplyCameraRotation()
    {
        transform.localRotation = Quaternion.Euler(currentTilt, currentPan, 0f);
    }

    public void LookAtPosition(Vector3 worldPosition)
    {
        // Point the camera at a specific world position (e.g. a ship's position)
        Vector3 direction = worldPosition - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Get local Euler angles from the target rotation
        Vector3 localAngles = (Quaternion.Inverse(cameraMount.rotation) * targetRotation).eulerAngles;

        // Normalize angles to -180 to 180 range
        localAngles.x = (localAngles.x > 180) ? localAngles.x - 360 : localAngles.x;
        localAngles.y = (localAngles.y > 180) ? localAngles.y - 360 : localAngles.y;

        currentTilt = Mathf.Clamp(localAngles.x, minTilt, maxTilt);
        currentPan = Mathf.Clamp(localAngles.y, -maxPan, maxPan);
    }

    void HandleCameraControl()
    {
        
        // Using WASD or arrow keys for pan/tilt control
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            PanLeft(rotationSpeed* Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            PanRight(rotationSpeed* Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            TiltUp(rotationSpeed* Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            TiltDown(rotationSpeed* Time.deltaTime);
        }

        //Using Q/E for zoom control
        if (Input.GetKey(KeyCode.Q))
        {
            ZoomIn(zoomSpeed* Time.deltaTime); // Zoom in
        }
        if (Input.GetKey(KeyCode.E))
        {
            ZoomOut(zoomSpeed* Time.deltaTime); // Zoom out
        }

        // Reset camera orientation with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCamera();
        }
    }
    
    private void HandleCapture()
    {
        bool capturePressed = false;

        if (Input.GetKeyDown(captureInputKey))
        {
            capturePressed = true;
        }

        if (useVRControllerInput)
        {
            // Going to use the Oculus Quest controller

        }

        if (capturePressed)
        {
            FlashScreen();
            CaptureImage();
        }
    }

    /// <summary>
    /// Camera Movement Functions
    /// </summary>
    
    public void PanLeft(float amount)
    {
        currentPan -= amount;
        currentPan = Mathf.Clamp(currentPan, -maxPan, maxPan);
    }

    public void PanRight(float amount)
    {
        currentPan += amount;
        currentPan = Mathf.Clamp(currentPan, -maxPan, maxPan);
    }

    public void TiltUp(float amount)
    {
        currentTilt += amount;
        currentTilt = Mathf.Clamp(currentTilt, minTilt, maxTilt);
    }

    public void TiltDown(float amount)
    {
        currentTilt -= amount;
        currentTilt = Mathf.Clamp(currentTilt, minTilt, maxTilt);
    }

    public void ZoomIn(float amount)
    {
        currentZoom -= amount;
        currentZoom = Mathf.Clamp(currentZoom, minFOV, maxFOV);
    }

    public void ZoomOut(float amount)
    {
        currentZoom += amount;
        currentZoom = Mathf.Clamp(currentZoom, minFOV, maxFOV);
    }

    public void SetZoom(float fov)
    {
        currentZoom = Mathf.Clamp(fov, minFOV, maxFOV);
    }

    public void ResetCamera()
    {
        currentPan = 0f;
        currentTilt = 15f;
        currentZoom = 45f;
    }

    /// <summary>
    /// Image Capture and Detection Functions
    /// </summary>
    
    public void CaptureImage()
    {
        UpdateStatusText("Capturing image...");

        SimulatedShip detectedShip = null;
        bool contactConfirmed = false;

        if (useYOLODetection && yoloDetector != null)
        {
            Texture2D capturedFrame = CaptureCameraFrame();
            try
            {
                yolodetector.Detection bestDetection;
                bool yoloFoundShip = yoloDetector.TryGetBestShipDetection(capturedFrame, out bestDetection);

                if (yoloFoundShip)
                {
                    detectedShip = requireRaycastHitForYOLOConfirmation ? RaycastDetectShip() : GetShipInView();
                    contactConfirmed = detectedShip != null;

                    if (contactConfirmed && requireExpectedContactMatch && expectedUnknownContact != null)
                    {
                        contactConfirmed = detectedShip == expectedUnknownContact;
                        if (!contactConfirmed)
                        {
                            UpdateStatusText("Contact rejected: captured vessel does not match the unknown contact label.");
                        }
                    }

                    if (contactConfirmed)
                    {
                        UpdateStatusText($"Contact confirmed ({bestDetection.confidence:P0})");
                    }
                    else
                    {
                        UpdateStatusText("YOLO detected vessel but contact was not centered. Rejected.");
                    }
                }
                else
                {
                    UpdateStatusText("Contact rejected: YOLO found no vessel in capture.");
                }
            }
            finally
            {
                if (capturedFrame != null)
                {
                    Destroy(capturedFrame);
                }
            }
        }
        else if (useRaycastDetection)
        {
            // Fallback mode when YOLO is disabled/unavailable.
            detectedShip = RaycastDetectShip();
            contactConfirmed = (detectedShip != null);
        }

        if (contactConfirmed && detectedShip != null)
        {
            HandleDetectionSuccess(detectedShip);
        }
        else
        {
            HandleDetectionFailure();
        }
    }

    private Texture2D CaptureCameraFrame()
    {
        RenderTexture rt = RenderTexture.GetTemporary(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousCameraTarget = eoirCamera.targetTexture;

        eoirCamera.targetTexture = rt;
        eoirCamera.Render();
        RenderTexture.active = rt;

        Texture2D frame = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        frame.Apply();

        eoirCamera.targetTexture = previousCameraTarget;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return frame;
    }

    private SimulatedShip RaycastDetectShip()
    {
        //Ray ray = eoirCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.SphereCast(eoirCamera.transform.position, 5f, eoirCamera.transform.forward, out hit, detectionRange))
        {
            SimulatedShip ship = hit.collider.GetComponent<SimulatedShip>();
            if (ship != null)
            {
                // Check if ship is centered in the camera view
                // Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(ship.transform.position);
                // float centerDistance = Vector2.Distance(
                //     new Vector2(viewPortPoint.x, viewPortPoint.y),
                //     new Vector2(0.5f, 0.5f)
                // );
                sphereCastTarget = ship.gameObject;
                UpdateStatusText($"Ship detected {ship.shipName} by Spherecast");
                // Ship has to be within 40% of the center of the view to be considered a valid detection
                // if (centerDistance < 0.4f) // Adjust this threshold as needed
                // {
                //     UpdateStatusText($"Ship detected {ship.shipName}");
                //     return ship;
                // }
                // else
                // {
                //     UpdateStatusText("Ship detected but not centered. Adjust camera aim.");
                //     return null;
                // }
            }
            else 
            {
                sphereCastTarget = null;
                UpdateStatusText("Spherecast hit an object but it is not a ship.");
            }
        }
        return null;
    }

    private SimulatedShip GetShipInView()
    {
        // This function would use the YOLO detection results to determine which SimulatedShip is in view
        // For simplicity, let's assume it returns the first ship it detects in the frame that is centered

        foreach (SimulatedShip ship in FindObjectsOfType<SimulatedShip>())
        {
            Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(ship.transform.position);
            bool inView = viewPortPoint.z > 0 && viewPortPoint.x >= 0 && viewPortPoint.x <= 1 && viewPortPoint.y >= 0 && viewPortPoint.y <= 1;
            float distance = Vector3.Distance(eoirCamera.transform.position, ship.transform.position);


            // If the ship is in view and the distance is within detection range, consider it detected
            if (inView && distance <= detectionRange)
            {
                return ship;
            }
        }
        return null;
    }

    /// <summary>
    /// UI Handling Functions
    /// </summary>
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            StartCoroutine(ShowStatusMessage(message, 3f)); // Show message for 3 seconds
        }
    }

    private void FlashScreen()
    {
        if (flashEffect != null)
        {
            StartCoroutine(Flash());
        }
    }

    private System.Collections.IEnumerator ShowStatusMessage(string message, float duration)
    {
        statusText.text = message;
        yield return new WaitForSeconds(duration);
        statusText.text = "";
    }

    private System.Collections.IEnumerator Flash()
    {
        // Flash the screen white to indicate a successful capture
        flashEffect.enabled = true;
        Color flashColor = flashEffect.color;
        flashColor.a = 0.8f;
        flashEffect.color = flashColor;

        yield return new WaitForSeconds(2); // Flash duration

        // Fade out the flash
        flashColor.a = 0f;
        flashEffect.color = flashColor;
        flashEffect.enabled = false;
    }

    /// <summary>
    /// Detection Handling Functions
    /// </summary>
    
    private void HandleDetectionSuccess(SimulatedShip ship)
    {
        string message = $"Ship Detected: {ship.shipName}";
        UpdateStatusText(message);

        Debug.Log($"[EO/IR] {message} (MMSI: {ship.mmsi})");
        OnShipDetected?.Invoke(ship);
    }

    private void HandleDetectionFailure()
    {
        string message = "No ship detected - aim camera at a ship and try again.";
        UpdateStatusText(message);

        Debug.Log($"[EO/IR] {message}");
        OnNoShipDetected?.Invoke();
    }

    /// <summary>
    /// Debug Visualization Functions using Gizmos
    /// </summary>
    
    void OnDrawGizmos()
    {
        if (eoirCamera == null) return;

        // Draw camera FOV
        Gizmos.color = Color.yellow;
        Gizmos.matrix = eoirCamera.transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, eoirCamera.fieldOfView, detectionRange, 0.1f, eoirCamera.aspect);

        // Draw center ray for raycast detection
        Gizmos.color = Color.red;
        Ray ray = eoirCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * detectionRange);
    }
}
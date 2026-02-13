using UnityEngine;
using UnityEngine.InputSystem;
using Data;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using System.Numerics;
using System.Drawing;

public class EOIRCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The EOIR camera that renders the ship scene")]
    public Camera eoirCamera;

    [Tooltip("Display screen for EOIR camera output")]
    public RenderTexture cameraDisplay;

    [Header("Camera Control Parameters")]
    [Tooltip("Pan speed (degrees per second)")]
    public float panSpeed = 30f;

    [Tooltip("Tilt speed (degrees per second)")]
    public float tiltSpeed = 30f;

    [Tooltip("Zoom speed (units per second)")]
    public float zoomSpeed = 5f;

    [Tooltip ("Minimum FOV (max zoom in)")]
    public float minFOV = 10f;

    [Tooltip("Maximum FOV (max zoom out)")]
    public float maxFOV = 60f;

    [Header("Detection Parameters")]

    [Tooltip("YOLO detector")]
    public YOLODetector yoloDetector;

    [Tooltip("Raycast detector")]
    public bool useRaycastDetection = true;

    [Tooltip("Detection range in meters")]
    public float detectionRange = 15000f;

    [Header("Input Settings")]
    [Tooltip("Input key to capture camera control input")]
    public KeyCode captureInputKey = KeyCode.Space;

    [Tooltip("VR controller input button to capture camera control input")]
    public bool useVRControllerInput = false;

    [Header("UI Elements")]
    public GameObject crosshairUI;
    public UnityEngine.UI.Text statusText;
    public UnityEngine.UI.Image flashEffect;

    //Internal state variables
    private float currentPan = 0f;
    private float currentTilt = 0f;

    // Event to notify when a ship is detected
    public event System.Action<SimulatedShip> OnShipDetected;
    public event System.Action OnNoShipDetected;

    void Start()
    {
        if (eoirCamera == null)
        {
            Debug.LogError("[EO/IR] No camera assigned!");
            enabled = false;
            return;
        }

        // Setup camera feed display
        if (cameraDisplay != null)
        {
            eoirCamera.targetTexture = cameraDisplay;
        }

        // Initialize camera orientation
        currentPan = transform.localEulerAngles.y;
        currentTilt = transform.localEulerAngles.x;

        UpdateStatusText("EO/IR Camera Ready");
    }

    void Update()
    {
        HandleCameraControl();
        HandleCapture();
    }

    /// <summary>
    /// Camera Handling Functions
    /// </summary>

    private void HandleCameraControl()
    {
        // Keyboard input for testing
        float panInput = 0f;
        float tiltInput = 0f;
        float zoomInput = 0f;

        // Using WASD or arrow keys for pan/tilt control
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            panInput = -1f;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            panInput = 1f;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            tiltInput = 1f;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            tiltInput = -1f;
        }

        //Using Q/E for zoom control
        if (Input.GetKey(KeyCode.Q))
        {
            zoomInput = 1f; // Zoom out
        }
        if (Input.GetKey(KeyCode.E))
        {
            zoomInput = -1f; // Zoom in
        }

        // Apply pan and tilt based on input
        if (panInput != 0f || tiltInput != 0f)
        {
            currentPan += panInput * panSpeed * Time.deltaTime;
            currentTilt += tiltInput * tiltSpeed * Time.deltaTime;

            // Clamp tilt to prevent flipping
            currentTilt = Mathf.Clamp(currentTilt, -90f, 90f);

            // Apply rotation to camera
            transform.localRotation = Quaternion.Euler(currentTilt, currentPan, 0f);
        }

        // Apply zoom based on input
        if (zoomInput != 0f)
        {
            float newFOV = eoirCamera.fieldOfView + zoomInput * zoomSpeed * Time.deltaTime;
            eoirCamera.fieldOfView = Mathf.Clamp(newFOV, minFOV, maxFOV);
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
            CaptureImage();
        }
    }


    /// <summary>
    /// Image Capture and Detection Functions
    /// </summary>
    
    public void CaptureImage()
    {
        UpdateStatusText("Capturing image...");

        bool shipDetected = false;
        SimulatedShip detectedShip = null;

        // First try YOLO detection if enabled
        if (yoloDetector != null)
        {
            shipDetected = yoloDetector.DetectShipInFrame(); //Need to implement this function in YOLODetector

            if (shipDetected)
            {
                detectedShip = GetShipInView();
            }
        }

        // If YOLO didn't detect anything and raycast detection is enabled, try raycast
        else if (useRaycastDetection)
        {
            detectedShip = RayCastDetectShip(); // Need to implement this function to raycast from camera center and check for SimulatedShip hits within detectionRange
            shipDetected = (detectedShip != null);
        }

        // Handle detection results
        if (shipDetected && detectedShip != null)
        {
            HandleDetectionSuccess(detectedShip);
        }
        else
        {
            HandleDetectionFailure();
        }
    }

    private SimulatedShip RaycastDetectShip()
    {
        Ray ray = eoirCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, detectionRange))
        {
            SimulatedShip ship = hit.collider.GetComponent<SimulatedShip>();
            if (ship != null)
            {
                // Check if ship is centered in the camera view
                Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(ship.transform.position);
                float centerDistance = Vector2.Distance(
                    new Vector2(viewPortPoint.X, viewPortPoint.Y),
                    new Vector2(0.5f, 0.5f)
                );
                // Ship has to be within 20% of the center of the view to be considered a valid detection
                if (centerDistance < 0.2f) // Adjust this threshold as needed
                {
                    return ship;
                }
                else
                {
                    UpdateStatusText("Ship detected but not centered. Adjust camera aim.");
                    return null;
                }
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
            statusText.text = message;
        }
    }

    private void FlashScreen()
    {
        if (flashEffect != null)
        {
            StartCoroutine(FlashCoroutine());
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        // Flash the screen white to indicate a successful capture
        flashEffect.enabled = true;
        Color flashColor = flashEffect.color;
        flashColor.A = 0.8f;
        flashEffect.color = flashColor;

        yield return new WaitForSeconds(0.1f); // Flash duration

        // Fade out the flash
        flashColor.A = 0f;
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
        Gizmos.color = Color.Yellow;
        Gizmos.matrix = eoirCamera.transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.Zero, eoirCamera.fieldOfView, detectionRange, 0.1f, eoirCamera.aspect);

        // Draw center ray for raycast detection
        Gizmos.color = Color.Red;
        Ray ray = eoirCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * detectionRange);
    }
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Data;
using UnityEngine.XR;

using TMPro;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
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

    [Header("UI Integration")]
    [Tooltip("Optional UI manager used to reflect capture/analyze/confirm/reject states")]
    public UIManager uiManager;

    [Header("Detection Parameters")]

    [Tooltip("YOLO detector used to classify captured EO/IR images")]
    public yolodetector yoloDetector;

    [Tooltip("Enable YOLO-based confirmation/rejection for capture events")]
    public bool useYOLODetection = false;

    [Tooltip("Run physics detection first; use YOLO only as secondary confirmation/fallback")]
    public bool usePhysicsDetection = true;

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

    [Tooltip("Detection range in meters (must exceed farthest scenario contact)")]
    public float detectionRange = 30000f;

    [Tooltip("Spherecast radius in meters used as tolerance for center-hit confirmation")]
    public float detectionSphereRadius = 45f;
    
    [Tooltip("Allow viewport-based fallback when physics confirmation misses tiny distant targets")]
    public bool useViewportFallbackDetection = true;
    
    [Tooltip("Max viewport distance from center for fallback detection (0.0-1.0)")]
    [Range(0.01f, 0.5f)]
    public float viewportFallbackCenterTolerance = 0.12f;

    [Tooltip("Viewport tolerance used when selecting among physics hit candidates")]
    [Range(0.01f, 0.5f)]
    public float physicsHitSelectionTolerance = 0.18f;

    [Header("YOLO Training Capture")]

    [Tooltip("Save each capture as YOLO training data (image + .txt labels)")]
    public bool saveYoloTrainingSamples = false;

    [Tooltip("Folder name under Application.persistentDataPath used for YOLO dataset export")]
    public string yoloDatasetFolderName = "yolo_dataset";

    [Tooltip("Store JPG images (true) or PNG images (false)")]
    public bool exportJpg = true;

    [Tooltip("JPG quality used when exportJpg is enabled")]
    [Range(1, 100)]
    public int jpgQuality = 95;

    [Tooltip("When false, captures with no visible ship labels are skipped")]
    public bool includeNegativeSamples = true;

    [Tooltip("Use one YOLO class (ship=0) instead of classes per SimulatedShip.shipType")]
    public bool useSingleShipClass = true;

    [Tooltip("Minimum normalized box size to include as label")]
    [Range(0.0001f, 0.1f)]
    public float minNormalizedBoxSize = 0.005f;

    /// <summary>
    /// YOLO dataset export paths and state
    /// </summary>
    private string yoloDatasetRootPath;
    private string yoloImagesPath;
    private string yoloLabelsPath;
    private int yoloSampleCounter;
    private bool yoloDatasetReady;

    [Header("Input Settings")]

    [Tooltip("Input key to capture camera control input")]
    public KeyCode captureInputKey = KeyCode.Space;

    [Tooltip("VR controller input button to capture camera control input")]
    public bool useVRControllerInput = true;

    [Tooltip("Automatically enable VR controller input when an XR device is active")]
    public bool autoEnableVRControllerInputWhenXRActive = true;

    [Header("Quest 2 Controller Settings")]
    [Tooltip("Deadzone used for right joystick camera movement")]
    [Range(0.01f, 0.5f)]
    public float rightStickDeadzone = 0.2f;

    [Tooltip("Trigger threshold used when reading the right trigger as a capture input")]
    [Range(0.05f, 1f)]
    public float rightTriggerThreshold = 0.2f;

    [Tooltip("Optional multiplier for right joystick pan/tilt speed")]
    public float rightStickSensitivity = 1f;

    [Tooltip("When enabled, EOIR stick/A/B input only works while holding right grip")]
    public bool requireRightGripForEOIRControl = false;

    [Tooltip("Fallback threshold if grip is exposed as an axis instead of a button")]
    [Range(0.1f, 1f)]
    public float rightGripAxisThreshold = 0.6f;

    [Header("Quest 2 Left Controller Ray")]
    [Tooltip("Show a debug ray for the left controller in VR")]
    public bool showLeftControllerRay = true;

    [Tooltip("Always show the left controller ray while Scenario 1 is active")]
    public bool forceLeftRayInScenario1 = true;

    [Tooltip("Ray length for the left controller selection ray")]
    public float leftControllerRayLength = 20000f;

    [Tooltip("Selection radius used for left-controller ship hit detection (bigger = easier to hit small distant ships)")]
    public float leftControllerSelectionRadius = 80f;

    [Tooltip("Ray width for the left controller line renderer")]
    public float leftControllerRayWidth = 0.03f;

    [Tooltip("Hit marker size for the left controller ray")]
    public float leftControllerRayHitMarkerSize = 0.04f;

    [Tooltip("Local rotation offset that aligns the ray with the controller forward direction")]
    public Vector3 leftControllerRayLocalEulerOffset = new Vector3(90f, 0f, 0f);

    [Tooltip("Trigger threshold used when reading the left trigger as a confirm input")]
    [Range(0.05f, 1f)]
    public float leftControllerTriggerThreshold = 0.2f;

    [Tooltip("Ray color when the left controller ray is idle")]
    public Color leftControllerRayColor = Color.cyan;

    [Tooltip("Ray color when the left trigger is held")]
    public Color leftControllerRayHitColor = Color.green;

    [Tooltip("Haptic amplitude when left trigger confirmation hits a ship collider")]
    [Range(0f, 1f)]
    public float leftRayHapticAmplitude = 0.6f;

    [Tooltip("Haptic duration when left trigger confirmation hits a ship collider")]
    [Range(0.01f, 0.3f)]
    public float leftRayHapticDuration = 0.06f;

    private InputDevice rightController;
    private InputDevice leftController;
    private bool previousAButtonState;
    private bool previousBButtonState;
    private bool previousRightTriggerState;
    private bool previousLeftTriggerState;
    private bool rightControllerActive;
    private bool leftControllerActive;
    private string lastConfirmedTrackId;
    private Quaternion initialLocalRotation;
    private float initialTilt;
    private float initialPan;
    private Transform leftControllerTransform;
    private LineRenderer leftControllerRay;
    private GameObject leftControllerRayHitMarker;
    private StudyScenarioController scenarioController;

    // Event to notify when a ship is detected
    public event System.Action<SimulatedShip> OnShipDetected;
    public event System.Action OnNoShipDetected;

    void Start()
    {
        EnsureVRControllerInputState();

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }

        if (scenarioController == null)
        {
            scenarioController = FindFirstObjectByType<StudyScenarioController>();
        }

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

        initialLocalRotation = transform.localRotation;
        initialTilt = currentTilt;
        initialPan = currentPan;

        // Initialize camera orientation
        eoirCamera.fieldOfView = currentZoom;

        // Need a function to set the initial rotation of the camera based on currentPan and currentTilt
        ApplyCameraRotation();

        UpdateStatusText("[EO/IR] Camera Ready");

        if (saveYoloTrainingSamples)
        {
            EnsureYoloDatasetFolders();
        }

        if (useVRControllerInput)
        {
            TryInitializeLeftController();
            InitializeLeftControllerRayVisualizer();
        }
        TryInitializeRightController();
    }

    void Update()
    {
        EnsureVRControllerInputState();

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }

        if (scenarioController == null)
        {
            scenarioController = FindFirstObjectByType<StudyScenarioController>();
        }

        HandleLeftControllerAISInput();
        HandleCameraControl();
        UpdateCamera();
        HandleCapture();
    }

    private void EnsureVRControllerInputState()
    {
        if (useVRControllerInput || !autoEnableVRControllerInputWhenXRActive)
        {
            return;
        }

        if (!XRSettings.isDeviceActive)
        {
            return;
        }

        useVRControllerInput = true;
    }

    void LateUpdate()
    {
        UpdateLeftControllerRayVisualizer();
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
        Quaternion deltaRotation = Quaternion.Euler(currentTilt - initialTilt, currentPan - initialPan, 0f);
        transform.localRotation = initialLocalRotation * deltaRotation;
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
#if !ENABLE_INPUT_SYSTEM
        // Using WASD or arrow keys for pan/tilt control (only on old Input Manager)
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
#endif

        if (useVRControllerInput)
        {
            HandleVRCameraControl();
        }
    }
    
    private void HandleCapture()
    {
        bool capturePressed = false;

#if !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(captureInputKey))
        {
            capturePressed = true;
        }
#endif

        if (useVRControllerInput)
        {
            if (IsEOIRControlActive())
            {
                capturePressed |= IsRightControllerButtonDown(CommonUsages.primaryButton, ref previousAButtonState);
            }
            else
            {
                // Keep edge-trigger state in sync so A does not fire when grip is pressed later.
                SyncRightControllerButtonState(CommonUsages.primaryButton, ref previousAButtonState);
            }

        }

        if (capturePressed)
        {
            if (uiManager != null)
            {
                uiManager.OnCaptureStarted();
            }
            FlashScreen();
            CaptureImage();
        }
    }

    private void HandleLeftControllerAISInput()
    {
        if (!useVRControllerInput || uiManager == null)
        {
            return;
        }

        if (uiManager.scenario != UIManager.StudyScenario.AISDeterministicBaseline)
        {
            return;
        }

        if (!TryIsLeftControllerTriggerPressed())
        {
            return;
        }

        SimulatedShip clickedShip = RaycastDetectShipFromLeftController();
        if (clickedShip != null)
        {
            TrySendLeftControllerHapticImpulse(leftRayHapticAmplitude, leftRayHapticDuration);
        }
        uiManager.TryConfirmAisTargetFromSelection(clickedShip);
    }

    private void HandleVRCameraControl()
    {
        if (!TryInitializeRightController())
        {
            return;
        }

        bool eoirControlActive = IsEOIRControlActive();

        if (!eoirControlActive)
        {
            // Keep edge-trigger state in sync so B does not fire when grip is pressed later.
            SyncRightControllerButtonState(CommonUsages.secondaryButton, ref previousBButtonState);
            return;
        }

        Vector2 rightStick;
        if (TryGetRightStickInput(out rightStick))
        {
            float horizontal = Mathf.Abs(rightStick.x) >= rightStickDeadzone ? rightStick.x : 0f;
            float vertical = Mathf.Abs(rightStick.y) >= rightStickDeadzone ? rightStick.y : 0f;

            if (!Mathf.Approximately(horizontal, 0f))
            {
                float panAmount = horizontal * rotationSpeed * rightStickSensitivity * Time.deltaTime;
                if (panAmount > 0f)
                {
                    PanRight(panAmount);
                }
                else
                {
                    PanLeft(-panAmount);
                }
            }

            if (!Mathf.Approximately(vertical, 0f))
            {
                float tiltAmount = vertical * rotationSpeed * rightStickSensitivity * Time.deltaTime;
                if (tiltAmount > 0f)
                {
                    TiltUp(tiltAmount);
                }
                else
                {
                    TiltDown(-tiltAmount);
                }
            }
        }

        // Right Trigger for zoom in
        if (TryIsRightTriggerHeld())
        {
            ZoomIn(zoomSpeed * Time.deltaTime);
        }

        // Right Grip for zoom out
        if (TryIsRightGripHeld())
        {
            ZoomOut(zoomSpeed * Time.deltaTime);
        }

        if (IsRightControllerButtonDown(CommonUsages.secondaryButton, ref previousBButtonState))
        {
            ResetCamera();
        }
    }

    private bool IsEOIRControlActive()
    {
        if (!useVRControllerInput)
        {
            return false;
        }

        if (!TryInitializeRightController())
        {
            return false;
        }

        if (!requireRightGripForEOIRControl)
        {
            return true;
        }

        bool gripButtonPressed;
        if (rightController.TryGetFeatureValue(CommonUsages.gripButton, out gripButtonPressed))
        {
            return gripButtonPressed;
        }

        float gripAxis;
        if (rightController.TryGetFeatureValue(CommonUsages.grip, out gripAxis))
        {
            return gripAxis >= rightGripAxisThreshold;
        }

        // Some Quest/OpenXR controller profiles do not expose grip values.
        // In that case, do not hard-block EOIR controls.
        return true;
    }

    /// <summary>
    /// VR Controller Input Handling Functions
    /// </summary>

    private bool TryInitializeRightController()
    {
        if (rightController.isValid)
        {
            return true;
        }

        // Preferred lookup for hand-specific devices.
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightController.isValid)
        {
            previousAButtonState = false;
            previousBButtonState = false;
            previousRightTriggerState = false;
            rightControllerActive = true;
            return true;
        }

        List<InputDevice> devices = new List<InputDevice>();

        // Some runtimes do not expose HeldInHand, so try multiple characteristic sets.
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right,
            devices);

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
                devices);
        }

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right, devices);
        }

        // Final fallback: use any controller device if handedness is not advertised.
        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        }

        if (devices.Count == 0)
        {
            if (!rightControllerActive)
            {
                Debug.LogWarning("[EO/IR] No righthand controller found for EO/IR input!");
                rightControllerActive = false;
            }
            return false;
        }

        int selectedIndex = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            InputDeviceCharacteristics chars = devices[i].characteristics;
            if ((chars & InputDeviceCharacteristics.Right) == InputDeviceCharacteristics.Right)
            {
                selectedIndex = i;
                break;
            }

            string deviceName = devices[i].name;
            if (!string.IsNullOrEmpty(deviceName) && deviceName.ToLowerInvariant().Contains("right"))
            {
                selectedIndex = i;
                break;
            }
        }

        rightController = devices[selectedIndex];
        rightControllerActive = true;
        previousAButtonState = false;
        previousBButtonState = false;
        previousRightTriggerState = false;
        return rightController.isValid;
    }

    private bool IsRightControllerButtonDown(InputFeatureUsage<bool> buttonUsage, ref bool previousButtonState)
    {
        if (!TryInitializeRightController())
        {
            previousButtonState = false;
            return false;
        }

        bool currentButtonState;
        if (!rightController.TryGetFeatureValue(buttonUsage, out currentButtonState))
        {
            previousButtonState = false;
            return false;
        }

        bool pressedThisFrame = currentButtonState && !previousButtonState;
        previousButtonState = currentButtonState;
        return pressedThisFrame;
    }

    private void SyncRightControllerButtonState(InputFeatureUsage<bool> buttonUsage, ref bool previousButtonState)
    {
        if (!TryInitializeRightController())
        {
            previousButtonState = false;
            return;
        }

        bool currentButtonState;
        if (!rightController.TryGetFeatureValue(buttonUsage, out currentButtonState))
        {
            previousButtonState = false;
            return;
        }

        previousButtonState = currentButtonState;
    }

    private bool IsRightControllerTriggerDown(ref bool previousTriggerState)
    {
        if (!TryInitializeRightController())
        {
            previousTriggerState = false;
            return false;
        }

        bool triggerButtonPressed;
        if (rightController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed))
        {
            bool pressedThisFrame = triggerButtonPressed && !previousTriggerState;
            previousTriggerState = triggerButtonPressed;
            return pressedThisFrame;
        }

        float triggerValue;
        if (!rightController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            previousTriggerState = false;
            return false;
        }

        bool currentTriggerState = triggerValue > Mathf.Clamp01(rightTriggerThreshold);
        bool pressedThisFrameFallback = currentTriggerState && !previousTriggerState;
        previousTriggerState = currentTriggerState;
        return pressedThisFrameFallback;
    }

    private void SyncRightControllerTriggerState(ref bool previousTriggerState)
    {
        if (!TryInitializeRightController())
        {
            previousTriggerState = false;
            return;
        }

        bool triggerButtonPressed;
        if (rightController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed))
        {
            previousTriggerState = triggerButtonPressed;
            return;
        }

        float triggerValue;
        if (!rightController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            previousTriggerState = false;
            return;
        }

        previousTriggerState = triggerValue > Mathf.Clamp01(rightTriggerThreshold);
    }

    private bool TryGetRightStickInput(out Vector2 rightStick)
    {
        rightStick = Vector2.zero;

        if (!TryInitializeRightController())
        {
            return false;
        }

        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick))
        {
            return true;
        }

        return rightController.TryGetFeatureValue(CommonUsages.secondary2DAxis, out rightStick);
    }

    private bool TryIsRightTriggerHeld()
    {
        if (!TryInitializeRightController())
        {
            return false;
        }

        bool triggerButtonPressed;
        if (rightController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed))
        {
            return triggerButtonPressed;
        }

        float triggerValue;
        if (!rightController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            return false;
        }

        return triggerValue > Mathf.Clamp01(rightTriggerThreshold);
    }

    private bool TryIsRightGripHeld()
    {
        if (!TryInitializeRightController())
        {
            return false;
        }

        bool gripButtonPressed;
        if (rightController.TryGetFeatureValue(CommonUsages.gripButton, out gripButtonPressed))
        {
            return gripButtonPressed;
        }

        float gripAxis;
        if (!rightController.TryGetFeatureValue(CommonUsages.grip, out gripAxis))
        {
            return false;
        }

        return gripAxis >= rightGripAxisThreshold;
    }

    private bool TryInitializeLeftController()
    {
        if (leftController.isValid)
        {
            return true;
        }

        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftController.isValid)
        {
            previousLeftTriggerState = false;
            leftControllerActive = true;
            return true;
        }

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left,
            devices);

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
                devices);
        }

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left, devices);
        }

        // Final fallback for runtimes that do not advertise handedness consistently.
        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        }

        if (devices.Count == 0)
        {
            if (!leftControllerActive)
            {
                Debug.LogWarning("[EO/IR] No left-hand controller found for EO/IR ray input!");
            }

            leftControllerActive = false;
            return false;
        }

        int selectedIndex = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            InputDeviceCharacteristics chars = devices[i].characteristics;
            if ((chars & InputDeviceCharacteristics.Left) == InputDeviceCharacteristics.Left)
            {
                selectedIndex = i;
                break;
            }

            string deviceName = devices[i].name;
            if (!string.IsNullOrEmpty(deviceName) && deviceName.ToLowerInvariant().Contains("left"))
            {
                selectedIndex = i;
                break;
            }
        }

        leftController = devices[selectedIndex];
        previousLeftTriggerState = false;
        leftControllerActive = true;
        return leftController.isValid;
    }

    private bool TryIsLeftControllerTriggerPressed()
    {
        if (!TryInitializeLeftController())
        {
            previousLeftTriggerState = false;
            return false;
        }

        bool triggerButtonPressed;
        if (leftController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed))
        {
            bool pressedThisFrame = triggerButtonPressed && !previousLeftTriggerState;
            previousLeftTriggerState = triggerButtonPressed;
            return pressedThisFrame;
        }

        float triggerValue;
        if (!leftController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            previousLeftTriggerState = false;
            return false;
        }

        bool currentTriggerState = triggerValue > Mathf.Clamp01(leftControllerTriggerThreshold);
        bool pressedThisFrameFallback = currentTriggerState && !previousLeftTriggerState;
        previousLeftTriggerState = currentTriggerState;
        return pressedThisFrameFallback;
    }

    private bool TryIsLeftControllerTriggerHeld()
    {
        if (!TryInitializeLeftController())
        {
            return false;
        }

        bool triggerButtonPressed;
        if (leftController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed))
        {
            return triggerButtonPressed;
        }

        float triggerValue;
        if (!leftController.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            return false;
        }

        return triggerValue > Mathf.Clamp01(leftControllerTriggerThreshold);
    }

    private SimulatedShip RaycastDetectShipFromLeftController()
    {
        Vector3 origin;
        Quaternion rotation;
        if (!TryGetLeftControllerPose(out origin, out rotation))
        {
            return null;
        }

        Vector3 direction = (rotation * Quaternion.Euler(leftControllerRayLocalEulerOffset)) * Vector3.forward;
        float castDistance = Mathf.Max(0.5f, leftControllerRayLength);
        float castRadius = Mathf.Max(0f, leftControllerSelectionRadius);
        RaycastHit[] hits = castRadius > 0f
            ? Physics.SphereCastAll(origin, castRadius, direction, castDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(origin, direction, castDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        SimulatedShip bestShip = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            SimulatedShip ship = ResolveShipFromHit(hits[i]);
            if (ship == null)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestShip = ship;
            }
        }

        return bestShip;
    }

    private void InitializeLeftControllerRayVisualizer()
    {
        TryResolveLeftControllerTransform();

        if (leftControllerRay == null)
        {
            GameObject lineObject = new GameObject("LeftControllerRay");
            leftControllerRay = lineObject.AddComponent<LineRenderer>();
            leftControllerRay.useWorldSpace = true;
            leftControllerRay.positionCount = 2;
            leftControllerRay.startWidth = leftControllerRayWidth;
            leftControllerRay.endWidth = leftControllerRayWidth;
            leftControllerRay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            leftControllerRay.receiveShadows = false;
            leftControllerRay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            leftControllerRay.numCornerVertices = 4;
            leftControllerRay.numCapVertices = 4;
            leftControllerRay.textureMode = LineTextureMode.Stretch;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
            {
                unlitShader = Shader.Find("Sprites/Default");
            }

            if (unlitShader != null)
            {
                Material lineMaterial = new Material(unlitShader);
                lineMaterial.color = leftControllerRayColor;
                ConfigureRayOverlayMaterial(lineMaterial);
                leftControllerRay.material = lineMaterial;
            }

            leftControllerRay.startColor = leftControllerRayColor;
            leftControllerRay.endColor = leftControllerRayColor;
            leftControllerRay.sortingOrder = 5000;
            leftControllerRay.enabled = false;
        }

        if (leftControllerRayHitMarker == null)
        {
            leftControllerRayHitMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftControllerRayHitMarker.name = "LeftControllerRayHitMarker";
            leftControllerRayHitMarker.transform.localScale = Vector3.one * leftControllerRayHitMarkerSize;

            Collider markerCollider = leftControllerRayHitMarker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }

            Renderer markerRenderer = leftControllerRayHitMarker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                Shader markerShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (markerShader == null)
                {
                    markerShader = Shader.Find("Sprites/Default");
                }

                if (markerShader != null)
                {
                    Material markerMaterial = new Material(markerShader);
                    markerMaterial.color = leftControllerRayHitColor;
                    markerRenderer.material = markerMaterial;
                }
                else
                {
                    markerRenderer.material.color = leftControllerRayHitColor;
                }
            }

            leftControllerRayHitMarker.SetActive(false);
        }

    }

    private void ConfigureRayOverlayMaterial(Material rayMaterial)
    {
        if (rayMaterial == null)
        {
            return;
        }

        // Render after most transparent geometry and ignore depth tests when supported.
        rayMaterial.renderQueue = 5000;

        if (rayMaterial.HasProperty("_ZTest"))
        {
            rayMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        if (rayMaterial.HasProperty("_ZWrite"))
        {
            rayMaterial.SetInt("_ZWrite", 0);
        }
    }

    private bool TryResolveLeftControllerTransform()
    {
        if (leftControllerTransform != null)
        {
            return true;
        }

        string[] exactNames =
        {
            "Left Controller",
            "LeftHand Controller",
            "Left Hand Controller",
            "LeftHand"
        };

        for (int i = 0; i < exactNames.Length; i++)
        {
            GameObject leftControllerObject = GameObject.Find(exactNames[i]);
            if (leftControllerObject != null)
            {
                leftControllerTransform = leftControllerObject.transform;
                return true;
            }
        }

        // Fallback: find the most likely left controller object by name.
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            string candidateName = candidate.name;
            if (string.IsNullOrEmpty(candidateName))
            {
                continue;
            }

            string lowered = candidateName.ToLowerInvariant();
            bool looksLeft = lowered.Contains("left") || lowered.Contains("lefthand") || lowered.Contains("left hand");
            bool looksController = lowered.Contains("controller") || lowered.Contains("hand");
            if (looksLeft && looksController)
            {
                leftControllerTransform = candidate;
                return true;
            }
        }

        return false;
    }

    private void UpdateLeftControllerRayVisualizer()
    {
        if (!useVRControllerInput)
        {
            return;
        }

        bool isScenario1 = uiManager != null && uiManager.scenario == UIManager.StudyScenario.AISDeterministicBaseline;
        bool shouldShowRay = isScenario1 && (forceLeftRayInScenario1 || showLeftControllerRay);

        // Ray is only shown in Scenario 1 (AIS Baseline)
        if (!shouldShowRay)
        {
            if (leftControllerRay != null)
            {
                leftControllerRay.enabled = false;
            }
            if (leftControllerRayHitMarker != null)
            {
                leftControllerRayHitMarker.SetActive(false);
            }
            return;
        }

        if (leftControllerRay == null || leftControllerRayHitMarker == null)
        {
            InitializeLeftControllerRayVisualizer();
        }

        if (leftControllerRay == null || leftControllerRayHitMarker == null)
        {
            return;
        }

        Vector3 origin;
        Quaternion rotation;
        if (!TryGetLeftControllerPose(out origin, out rotation))
        {
            leftControllerRay.enabled = false;
            leftControllerRayHitMarker.SetActive(false);
            return;
        }

        Vector3 direction = (rotation * Quaternion.Euler(leftControllerRayLocalEulerOffset)) * Vector3.forward;
        Vector3 endPoint = origin + direction * Mathf.Max(0.5f, leftControllerRayLength);
        bool leftTriggerHeld = TryIsLeftControllerTriggerHeld();

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(
            origin,
            direction,
            out hit,
            Mathf.Max(0.5f, leftControllerRayLength),
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (hitSomething)
        {
            endPoint = hit.point;
            leftControllerRayHitMarker.SetActive(true);
            leftControllerRayHitMarker.transform.position = hit.point;
        }
        else
        {
            leftControllerRayHitMarker.SetActive(false);
        }

        Color rayColor = leftTriggerHeld ? leftControllerRayHitColor : leftControllerRayColor;
        leftControllerRay.startColor = rayColor;
        leftControllerRay.endColor = rayColor;

        leftControllerRay.enabled = true;
        leftControllerRay.SetPosition(0, origin);
        leftControllerRay.SetPosition(1, endPoint);
    }

    private void TrySendLeftControllerHapticImpulse(float amplitude, float duration)
    {
        if (!TryInitializeLeftController())
        {
            return;
        }

        HapticCapabilities capabilities;
        if (!leftController.TryGetHapticCapabilities(out capabilities) || !capabilities.supportsImpulse)
        {
            return;
        }

        float clampedAmplitude = Mathf.Clamp01(amplitude);
        float clampedDuration = Mathf.Max(0.01f, duration);
        leftController.SendHapticImpulse(0u, clampedAmplitude, clampedDuration);
    }

    private bool TryGetLeftControllerPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        // Prefer the tracked controller Transform because it is already in world space.
        // This matches the behavior that was reliable when the flow lived in UIManager/XR rig space.
        if (TryResolveLeftControllerTransform())
        {
            position = leftControllerTransform.position;
            rotation = leftControllerTransform.rotation;
            return true;
        }

        if (TryInitializeLeftController())
        {
            Vector3 localPosition;
            Quaternion localRotation;
            bool hasPosition = leftController.TryGetFeatureValue(CommonUsages.devicePosition, out localPosition);
            bool hasRotation = leftController.TryGetFeatureValue(CommonUsages.deviceRotation, out localRotation);
            if (hasPosition && hasRotation)
            {
                Transform trackingRoot = null;
                if (Camera.main != null)
                {
                    trackingRoot = Camera.main.transform.parent;
                }

                if (trackingRoot == null && eoirCamera != null)
                {
                    trackingRoot = eoirCamera.transform.parent;
                }

                if (trackingRoot != null)
                {
                    position = trackingRoot.TransformPoint(localPosition);
                    rotation = trackingRoot.rotation * localRotation;
                }
                else
                {
                    // Last-resort fallback for scenes with no obvious tracking-root transform.
                    position = localPosition;
                    rotation = localRotation;
                }

                return true;
            }
        }

        return false;
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
        currentTilt -= amount;
        currentTilt = Mathf.Clamp(currentTilt, minTilt, maxTilt);
    }

    public void TiltDown(float amount)
    {
        currentTilt += amount;
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

        if (uiManager != null)
        {
            uiManager.OnAnalyzing();
        }

        SimulatedShip detectedShip = null;
        bool contactConfirmed = false;
        Texture2D capturedFrame = null;

        bool tryPhysicsFirst = useRaycastDetection && (usePhysicsDetection || !useYOLODetection || yoloDetector == null);
        if (tryPhysicsFirst)
        {
            detectedShip = RaycastDetectShip();
            contactConfirmed = IsExpectedUnknownContact(detectedShip);
        }

        bool yoloRequested = !contactConfirmed && useYOLODetection && yoloDetector != null;
        bool trainingExportRequested = saveYoloTrainingSamples;
        if (yoloRequested || trainingExportRequested)
        {
            capturedFrame = CaptureCameraFrame();
        }

        if (yoloRequested)
        {
            yolodetector.Detection bestDetection;
            bool yoloFoundShip = yoloDetector.TryGetBestShipDetection(capturedFrame, out bestDetection);

            if (yoloFoundShip)
            {
                detectedShip = requireRaycastHitForYOLOConfirmation ? RaycastDetectShip() : GetShipInView();
                contactConfirmed = IsExpectedUnknownContact(detectedShip);

                if (contactConfirmed)
                {
                    string confidenceLevel = bestDetection.confidence > 0.8f ? "high" : bestDetection.confidence > 0.6f ? "medium" : "low";
                    UpdateStatusText($"✓ Confirmed ({confidenceLevel} confidence)");
                }
                else
                {
                    UpdateStatusText("✗ Wrong contact – aim for the assigned target.");
                }
            }
            else
            {
                UpdateStatusText("✗ No vessel detected in frame – adjust aim and retry.");

                if (useRaycastDetection && !tryPhysicsFirst)
                {
                    detectedShip = RaycastDetectShip();
                    contactConfirmed = IsExpectedUnknownContact(detectedShip);
                }
            }
        }
        else if (!contactConfirmed && useRaycastDetection)
        {
            // Fallback mode when YOLO is disabled/unavailable.
            detectedShip = RaycastDetectShip();
            contactConfirmed = IsExpectedUnknownContact(detectedShip);
        }

        if (contactConfirmed && detectedShip != null)
        {
            HandleDetectionSuccess(detectedShip);
        }
        else
        {
            HandleDetectionFailure(detectedShip);
        }

        if (saveYoloTrainingSamples && capturedFrame != null)
        {
            SaveYoloTrainingSample(capturedFrame);
        }

        if (capturedFrame != null)
        {
            Destroy(capturedFrame);
        }
    }

    private void EnsureYoloDatasetFolders()
    {
        if (yoloDatasetReady)
        {
            return;
        }

        yoloDatasetRootPath = Path.Combine(Application.persistentDataPath, yoloDatasetFolderName);
        yoloImagesPath = Path.Combine(yoloDatasetRootPath, "images");
        yoloLabelsPath = Path.Combine(yoloDatasetRootPath, "labels");

        Directory.CreateDirectory(yoloDatasetRootPath);
        Directory.CreateDirectory(yoloImagesPath);
        Directory.CreateDirectory(yoloLabelsPath);

        WriteYoloClassesFile();

        yoloSampleCounter = Directory.GetFiles(yoloImagesPath).Length;
        yoloDatasetReady = true;

        Debug.Log($"[EO/IR] YOLO export ready at: {yoloDatasetRootPath}");
    }

    private void WriteYoloClassesFile()
    {
        string classesPath = Path.Combine(yoloDatasetRootPath, "classes.txt");
        if (useSingleShipClass)
        {
            File.WriteAllLines(classesPath, new[] { "ship" }, Encoding.UTF8);
            return;
        }

        SimulatedShip.ShipType[] shipTypes = (SimulatedShip.ShipType[])System.Enum.GetValues(typeof(SimulatedShip.ShipType));
        string[] classNames = new string[shipTypes.Length];
        for (int i = 0; i < shipTypes.Length; i++)
        {
            classNames[i] = shipTypes[i].ToString().ToLowerInvariant();
        }

        File.WriteAllLines(classesPath, classNames, Encoding.UTF8);
    }

    private void SaveYoloTrainingSample(Texture2D frame)
    {
        if (frame == null)
        {
            return;
        }

        EnsureYoloDatasetFolders();

        List<string> yoloLabels = BuildYoloLabelLines();
        if (!includeNegativeSamples && yoloLabels.Count == 0)
        {
            return;
        }

        string sampleName = $"capture_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}_{yoloSampleCounter:D6}";
        string imageExtension = exportJpg ? ".jpg" : ".png";
        string imagePath = Path.Combine(yoloImagesPath, sampleName + imageExtension);
        string labelPath = Path.Combine(yoloLabelsPath, sampleName + ".txt");

        byte[] imageBytes = exportJpg ? frame.EncodeToJPG(jpgQuality) : frame.EncodeToPNG();
        File.WriteAllBytes(imagePath, imageBytes);
        File.WriteAllLines(labelPath, yoloLabels, Encoding.UTF8);

        yoloSampleCounter++;
        Debug.Log($"[EO/IR] YOLO sample saved: {sampleName} ({yoloLabels.Count} labels)");
    }

    private List<string> BuildYoloLabelLines()
    {
        List<string> labels = new List<string>();
        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();

        for (int i = 0; i < ships.Length; i++)
        {
            SimulatedShip ship = ships[i];
            if (ship == null)
            {
                continue;
            }

            Rect viewportBounds;
            if (!TryGetShipViewportRect(ship, out viewportBounds))
            {
                continue;
            }

            float width = viewportBounds.width;
            float height = viewportBounds.height;
            if (width < minNormalizedBoxSize || height < minNormalizedBoxSize)
            {
                continue;
            }

            float centerX = viewportBounds.x + width * 0.5f;
            float centerY = viewportBounds.y + height * 0.5f;
            int classId = GetYoloClassId(ship);

            string line = string.Format(CultureInfo.InvariantCulture, "{0} {1:F6} {2:F6} {3:F6} {4:F6}", classId, centerX, centerY, width, height);
            labels.Add(line);
        }

        return labels;
    }

    private int GetYoloClassId(SimulatedShip ship)
    {
        if (useSingleShipClass || ship == null)
        {
            return 0;
        }

        return Mathf.Max(0, (int)ship.shipType);
    }

    private bool TryGetShipViewportRect(SimulatedShip ship, out Rect viewportRect)
    {
        viewportRect = new Rect();
        if (ship == null || eoirCamera == null)
        {
            return false;
        }

        Bounds bounds;
        if (!TryGetShipBounds(ship, out bounds))
        {
            return false;
        }

        Vector3[] corners = new Vector3[8];
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(min.x, min.y, max.z);
        corners[2] = new Vector3(min.x, max.y, min.z);
        corners[3] = new Vector3(min.x, max.y, max.z);
        corners[4] = new Vector3(max.x, min.y, min.z);
        corners[5] = new Vector3(max.x, min.y, max.z);
        corners[6] = new Vector3(max.x, max.y, min.z);
        corners[7] = new Vector3(max.x, max.y, max.z);

        float minX = 1f;
        float maxX = 0f;
        float minY = 1f;
        float maxY = 0f;
        bool hasVisiblePoint = false;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 vp = eoirCamera.WorldToViewportPoint(corners[i]);
            if (vp.z <= 0f)
            {
                continue;
            }

            hasVisiblePoint = true;
            minX = Mathf.Min(minX, vp.x);
            maxX = Mathf.Max(maxX, vp.x);
            minY = Mathf.Min(minY, vp.y);
            maxY = Mathf.Max(maxY, vp.y);
        }

        if (!hasVisiblePoint)
        {
            return false;
        }

        minX = Mathf.Clamp01(minX);
        maxX = Mathf.Clamp01(maxX);
        minY = Mathf.Clamp01(minY);
        maxY = Mathf.Clamp01(maxY);

        float width = maxX - minX;
        float height = maxY - minY;
        if (width <= 0f || height <= 0f)
        {
            return false;
        }

        viewportRect = new Rect(minX, minY, width, height);
        return true;
    }

    private bool TryGetShipBounds(SimulatedShip ship, out Bounds bounds)
    {
        Collider[] colliders = ship.GetComponentsInChildren<Collider>();
        bool initialized = false;
        bounds = new Bounds(ship.transform.position, Vector3.zero);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = col.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return initialized;
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
        Ray centerRay = eoirCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Prefer exact center hits first, but score all candidates by reticle alignment.
        RaycastHit[] rayHits = Physics.RaycastAll(centerRay, detectionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        SimulatedShip raycastShip = SelectBestShipFromHits(rayHits, physicsHitSelectionTolerance);
        if (raycastShip != null)
        {
            return raycastShip;
        }
        
        // Fallback to sphere around reticle and again choose best aligned candidate.
        RaycastHit[] sphereHits = Physics.SphereCastAll(centerRay, detectionSphereRadius, detectionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        SimulatedShip sphereShip = SelectBestShipFromHits(sphereHits, physicsHitSelectionTolerance);
        if (sphereShip != null)
        {
            return sphereShip;
        }
        
        if (useViewportFallbackDetection)
        {
            SimulatedShip fallbackShip = GetBestShipNearReticle(viewportFallbackCenterTolerance);
            if (fallbackShip != null)
            {
                return fallbackShip;
            }
        }
        
        return null;
    }
    
    private SimulatedShip ResolveShipFromHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return null;
        }
        
        SimulatedShip ship = hit.collider.GetComponent<SimulatedShip>();
        if (ship != null)
        {
            return ship;
        }
        
        return hit.collider.GetComponentInParent<SimulatedShip>();
    }

    private SimulatedShip SelectBestShipFromHits(RaycastHit[] hits, float maxCenterDistance)
    {
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        HashSet<int> visitedShips = new HashSet<int>();
        SimulatedShip bestShip = null;
        float bestCenterDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            SimulatedShip ship = ResolveShipFromHit(hits[i]);
            if (ship == null)
            {
                continue;
            }

            int shipId = ship.GetInstanceID();
            if (visitedShips.Contains(shipId))
            {
                continue;
            }
            visitedShips.Add(shipId);

            Vector3 worldRef = GetShipReferencePoint(ship);
            Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(worldRef);
            if (viewPortPoint.z <= 0f)
            {
                continue;
            }

            float centerDistance = Vector2.Distance(new Vector2(viewPortPoint.x, viewPortPoint.y), new Vector2(0.5f, 0.5f));
            if (centerDistance <= maxCenterDistance && centerDistance < bestCenterDistance)
            {
                bestCenterDistance = centerDistance;
                bestShip = ship;
            }
        }

        return bestShip;
    }

    private Vector3 GetShipReferencePoint(SimulatedShip ship)
    {
        Collider shipCollider = ship.GetComponentInChildren<Collider>();
        if (shipCollider != null)
        {
            return shipCollider.bounds.center;
        }

        return ship.transform.position;
    }
    
    private SimulatedShip GetBestShipNearReticle(float maxCenterDistance)
    {
        SimulatedShip bestShip = null;
        float bestCenterDistance = float.MaxValue;
        
        foreach (SimulatedShip ship in FindObjectsOfType<SimulatedShip>())
        {
            if (ship == null)
            {
                continue;
            }
            
            Vector3 worldRef = GetShipReferencePoint(ship);
            Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(worldRef);
            bool inView = viewPortPoint.z > 0f && viewPortPoint.x >= 0f && viewPortPoint.x <= 1f && viewPortPoint.y >= 0f && viewPortPoint.y <= 1f;
            if (!inView)
            {
                continue;
            }
            
            float distance = Vector3.Distance(eoirCamera.transform.position, worldRef);
            if (distance > detectionRange)
            {
                continue;
            }
            
            float centerDistance = Vector2.Distance(new Vector2(viewPortPoint.x, viewPortPoint.y), new Vector2(0.5f, 0.5f));
            if (centerDistance <= maxCenterDistance && centerDistance < bestCenterDistance)
            {
                bestCenterDistance = centerDistance;
                bestShip = ship;
            }
        }
        
        return bestShip;
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
        string message = $"✓ {ship.shipName} confirmed";
        UpdateStatusText(message);

        Debug.Log($"[EO/IR] {message} (MMSI: {ship.mmsi})");

        if (uiManager != null)
        {
            string trackId = BuildTrackIdForShip(ship);
            lastConfirmedTrackId = trackId;
            uiManager.OnShipConfirmed(trackId);
        }

        OnShipDetected?.Invoke(ship);
    }

    private void HandleDetectionFailure(SimulatedShip detectedShip)
    {
        string message = "Unable to confirm – check aim and retry.";
        UpdateStatusText(message);

        Debug.Log($"[EO/IR] {message}");

        if (uiManager != null)
        {
            // Update the UI for the ship currently in view if possible; otherwise use a generic rejection id.
            string rejectedTrackId = BuildTrackIdForShip(detectedShip);

            if (string.IsNullOrEmpty(rejectedTrackId))
            {
                rejectedTrackId = BuildTrackIdForShip(expectedUnknownContact);
            }

            if (string.IsNullOrEmpty(rejectedTrackId))
            {
                rejectedTrackId = "NO_VALID_UNKNOWN_CONTACT";
            }

            if (!string.IsNullOrEmpty(rejectedTrackId))
            {
                uiManager.OnNoShipDetected(rejectedTrackId);
            }
        }

        OnNoShipDetected?.Invoke();
    }

    private string BuildTrackIdForShip(SimulatedShip ship)
    {
        if (ship == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(ship.mmsi))
        {
            return $"AIS_{ship.mmsi}";
        }

        return ship.shipName;
    }

    private bool IsExpectedUnknownContact(SimulatedShip ship)
    {
        return ship != null && !ship.aisTransponder;
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

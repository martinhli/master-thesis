using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Data;
using System.ComponentModel.Design;
using System.Numerics;
using System.ComponentModel.DataAnnotations;

public class aroverlayvalidator : MonoBehaviour
{
    [Header("References")]
    public TrackManager trackManager;
    public Camera mainCamera;

    [Header("Validation Settings")]
    [Tooltip("Run validation every N seconds")]
    public float validationInterval = 1f;

    [Tooltip("Maximum acceptable pixel error in pixels")]
    public float maxPixelError = 50f;

    [Tooltip("Maximum acceptable angular error in degrees")]
    public float maxAngularError = 2f;

    [Header("Debug Visualization Settings")]
    public bool showDebugLines = true;
    public bool showErrorText = true;

    [Header("Statistics")]
    public ValidationStats stats = new ValidationStats();

    private float timeSinceLastValidation = 0f;

    private List<ValidationResult> validationResults = new List<ValidationResult>();

    [System.Serializable]
    public class ValidationStats
    {
        public int totalValidations = 0;
        public int accurateProjections = 0;
        public int inaccurateProjections = 0;

        public float totalPixelError = 0f;
        public float totalAngularError = 0f;

        public float total3DPositionError = 0f;

        public float maxPixelError = 0f;
        public string worstTrackId = "";

        public float accuracyPercentage { get; set;}
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (Time.time - timeSinceLastValidation >= validationInterval)
        {
            ValidateOverlays();
            timeSinceLastValidation = Time.time;
        }
    }

    void ValidateOverlays()
    {
        // Check if track manager and camera are available
        if (trackManager == null || mainCamera == null)
        {
            return;
        }

        var activeTracks = trackManager.GetActiveTracks();

        foreach (var track in activeTracks)
        {
            // Skip if ship data is not available
            if (track.shipData == null)
                continue;

            // Find the actual SimulatedShip in the scene that corresponds to this track
            SimulatedShip groundTruth = FindGroundTruthShip(track);

            if (groundTruth != null)
            {
                // Validate the track overlay against the ground truth ship
                ValidateTrackOverlay(track, groundTruth);
            }
        }

        // After validating all tracks, calculate overall statistics
        UpdateStatistics();
    }

    private void ValidateTrackOverlay(Track track, SimulatedShip groundTruth)
    {
        // Ground truth position
        Vector3 truePosition = groundTruth.transform.position;

        // Track's estimated position
        Vector3 trackPosition = track.position;

        // Calculate errors as needed for statistics
        ValidationResult result = new ValidationResult
        {
            trackId = track.trackid,
            timestamp = Time.time
        };

        // 1. 3D Position Error
        result.positionError = Vector3.Distance(truePosition, trackPosition);
        // 2. 2D Screen Projection error
        Vector3 trueScreenPos = mainCamera.WorldToScreenPoint(truePosition);
        Vector3 trackScreenPos = mainCamera.WorldToScreenPoint(trackPosition);
        result.pixelError = Vector2.Distance(new Vector2(trueScreenPos.x, trueScreenPos.y), new Vector2(trackScreenPos.x, trackScreenPos.y));
        // 3. Angular error
        Vector3 trueDirection = (truePosition - mainCamera.transform.position).normalized;
        Vector3 trackDirection = (trackPosition - mainCamera.transform.position).normalized;
        result.angularError = Vector3.Angle(trueDirection, trackDirection);
        // 4. Check if is in view
        result.isInView = IsInCameraView(trueScreenPos);
        // 5. Distance from camera
        result.distanceFromCamera = Vector3.Distance(mainCamera.transform.position, truePosition);
        // Store results in the ValidationResult object for later statistics calculation
        validationHistory.Add(result);
        
    }


    SimulatedShip FindGroundTruthShip(Track track)
    {
        // This method should find the actual SimulatedShip in the scene that corresponds to the given track
        // For simplicity, we will assume that the track's shipData has a unique identifier that matches a SimulatedShip in the scene

        var allShips = FindObjectsOfType<SimulatedShip>();
        foreach (var ship in allShips)
        {
            if (ship.shipData != null && ship.shipData.mmsi == track.shipData.mmsi)
            {
                return ship;
            }
        }
        return null; // No matching ship found
    }

    private bool IsInCameraView(Vector3 screenPos)
    {
        return screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width && screenPos.y >= 0 && screenPos.y <= Screen.height;
    }

    
}
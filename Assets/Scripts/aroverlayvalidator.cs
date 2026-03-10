using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Data;
using System.ComponentModel.Design;

public class aroverlayvalidator : MonoBehaviour
{
    [Header("References")]
    public TrackManager trackManager;
    public Camera mainCamera;

    [Header("Validation Settings")]
    [Tooltip("Run validation every N seconds")]
    public float validationInterval = 1f;
    private float timeSinceLastValidation = 0f;

    [Tooltip("Maximum acceptable pixel error in pixels")]
    public float maxPixelError = 50f;

    [Tooltip("Maximum acceptable angular error in degrees")]
    public float maxAngularError = 2f;

    [Tooltip("Maximum acceptable 3D position error in meters")]
    public float maxPositionError = 30f;

    [Header("Debug Visualization Settings")]
    public bool showDebugLines = true;
    public bool showErrorText = true;

    [Header("Statistics")]
    public ValidationStats stats = new ValidationStats();

    private List<ValidationResult> validationHistory = new List<ValidationResult>();

    [System.Serializable]
    public class ValidationResult
    {
        public string trackId;
        public float timestamp;
        public float positionError;
        public float pixelError;
        public float angularError;
        public bool isInView;
        public float distanceFromCamera;
    }

    [System.Serializable]
    public class ValidationStats
    {
        public int totalValidations = 0;
        public int accurateProjections = 0;
        public int inaccurateProjections = 0;

        public float totalPixelError = 0f;
        public float totalAngularError = 0f;

        public float total3DPositionError = 0f;

        public float averagePixelError = 0f;
        public float averageAngularError = 0f;
        public float average3DError = 0f;

        public float maxPixelError = 0f;
        public string worstTrackId = "";

        public float accuracyPercentage { get; set;}
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        timeSinceLastValidation = Time.time;
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
            // Skip invalid track entries
            if (track == null)
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

        // Add label height to true position for more accurate screen projection comparison
        float labelHeight = 20f; // Adjust as needed based on your label's height in world units
        Vector3 truePositionWithLabel = truePosition + Vector3.up * labelHeight;
        Vector3 trackPositionWithLabel = trackPosition + Vector3.up * labelHeight;

        // Calculate errors as needed for statistics
        ValidationResult result = new ValidationResult
        {
            trackId = track.trackid,
            timestamp = Time.time
        };

        // 1. 3D Position Error
        result.positionError = Vector3.Distance(truePositionWithLabel, trackPositionWithLabel);
        // 2. 2D Screen Projection error
        Vector3 trueScreenPos = mainCamera.WorldToScreenPoint(truePositionWithLabel);
        Vector3 trackScreenPos = mainCamera.WorldToScreenPoint(trackPositionWithLabel);
        result.pixelError = Vector2.Distance(new Vector2(trueScreenPos.x, trueScreenPos.y), new Vector2(trackScreenPos.x, trackScreenPos.y));
        // 3. Angular error
        Vector3 trueDirection = (truePosition - mainCamera.transform.position).normalized;
        Vector3 trackDirection = (trackPosition - mainCamera.transform.position).normalized;
        result.angularError = Vector3.Angle(trueDirection, trackDirection);
        // 4. Check if is in view
        result.isInView = IsInCameraView(trueScreenPos);
        // 5. Distance from camera
        result.distanceFromCamera = Vector3.Distance(mainCamera.transform.position, truePositionWithLabel);
        // Store results in the ValidationResult object for later statistics calculation
        validationHistory.Add(result);
        // Update running stats
        stats.totalValidations++;

        bool isAccurate = result.pixelError <= maxPixelError
                          && result.angularError <= maxAngularError
                          && result.positionError <= maxPositionError;

        if (isAccurate)
            stats.accurateProjections++;
        else
            stats.inaccurateProjections++;

        stats.totalPixelError += result.pixelError;
        stats.totalAngularError += result.angularError;
        stats.total3DPositionError += result.positionError;

        // Track worst case
        if (result.pixelError > stats.maxPixelError)
        {
            stats.maxPixelError = result.pixelError;
            stats.worstTrackId = track.trackid;
        }

         // Log significant errors
        if (result.pixelError > maxPixelError)
        {
            Debug.LogWarning($"[Validation] High pixel error for {track.trackid}: " +
                           $"{result.pixelError:F1}px (3D error: {result.positionError:F1}m)");
        }
    }


    SimulatedShip FindGroundTruthShip(Track track)
    {
        // This method should find the actual SimulatedShip in the scene that corresponds to the given track
        // Match by MMSI between Track.shipData and SimulatedShip identity fields.

        if (track == null)
        {
            return null;
        }

        var allShips = FindObjectsOfType<SimulatedShip>();
        if (allShips == null || allShips.Length == 0)
        {
            return null;
        }

        // Prefer exact MMSI match when identity is available (AIS / fused tracks).
        if (track.shipData != null && !string.IsNullOrEmpty(track.shipData.mmsi))
        {
            foreach (var ship in allShips)
            {
                if (ship != null && !string.IsNullOrEmpty(ship.mmsi) && ship.mmsi == track.shipData.mmsi)
                {
                    return ship;
                }
            }
        }
        // Look for closest ship as fallback (Radar)
        SimulatedShip closestShip = null;
        float closestDistance = float.MaxValue;
        float distanceThreshold = 200f;

        foreach (var ship in allShips)
        {
            float distance = Vector2.Distance(new Vector2(ship.transform.position.x, ship.transform.position.z), new Vector2(track.position.x, track.position.z));
            if (distance < closestDistance && distance < distanceThreshold)
            {
                closestDistance = distance;
                closestShip = ship;
            }
        }

        return closestShip; // Return the closest matching ship or null if none found
    }

    private bool IsInCameraView(Vector3 screenPos)
    {
        return screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width && screenPos.y >= 0 && screenPos.y <= Screen.height;
    }

    public void ExportToCSV(string filename)
    {
        System.Text.StringBuilder csv = new System.Text.StringBuilder();
        
        // Header
        csv.AppendLine("Timestamp,TrackID,PixelError,AngularError,3DError,Distance,InView");
        
        // Data
        foreach (var result in validationHistory)
        {
            csv.AppendLine($"{result.timestamp:F2}," +
                          $"{result.trackId}," +
                          $"{result.pixelError:F2}," +
                          $"{result.angularError:F4}," +
                          $"{result.positionError:F2}," +
                          $"{result.distanceFromCamera:F2}," +
                          $"{result.isInView}");
        }
        
        string path = System.IO.Path.Combine(Application.dataPath, filename);
        System.IO.File.WriteAllText(path, csv.ToString());
        
        Debug.Log($"[Validation] Exported {validationHistory.Count} results to {path}");
    }

    public void PrintValidationReport()
    {
        Debug.Log("=== AR OVERLAY VALIDATION REPORT ===");
        Debug.Log($"Total Validations: {stats.totalValidations}");
        Debug.Log($"Accuracy: {stats.accuracyPercentage:F1}% (pixel <= {maxPixelError:F1}px, angular <= {maxAngularError:F2}deg, 3D <= {maxPositionError:F1}m)");
        Debug.Log($"Average Pixel Error: {stats.averagePixelError:F2}px");
        Debug.Log($"Average Angular Error: {stats.averageAngularError:F4}°");
        Debug.Log($"Average 3D Position Error: {stats.average3DError:F2}m");
        Debug.Log($"Max Pixel Error: {stats.maxPixelError:F2}px (Track: {stats.worstTrackId})");
        Debug.Log($"Accurate: {stats.accurateProjections} | Inaccurate: {stats.inaccurateProjections}");
    }

    public void ResetStatistics()
    {
        stats = new ValidationStats();
        validationHistory.Clear();
        Debug.Log("[Validation] Statistics reset");
    }

    private void UpdateStatistics()
    {
        if (stats.totalValidations == 0)
            return;
        
        stats.averagePixelError = stats.totalPixelError / stats.totalValidations;
        stats.averageAngularError = stats.totalAngularError / stats.totalValidations;
        stats.average3DError = stats.total3DPositionError / stats.totalValidations;
        stats.accuracyPercentage = (stats.accurateProjections / (float)stats.totalValidations) * 100f;
    }

    void OnDestroy()
    {
        PrintValidationReport();
        ExportToCSV("validation_report.csv");
    }    
}
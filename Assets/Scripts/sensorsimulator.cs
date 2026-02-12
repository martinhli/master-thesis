using System;
using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Threading.Tasks.Dataflow;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Numerics;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

public class SensorSimulator : MonoBehaviour
{
    [Header("References")]
    public TrackManager trackManager;
    public Transform aircraftTransform;

    public Camera eoirCamera;

    [Header("Ship Scene Objects")]
    public List<SimulatedShip> simulatedShips = new List<SimulatedShip>();

    [Header("AIS Sensor Parameters")]
    [Tooltip("AIS update interval in seconds")]
    public float aisUpdateInterval = 5f;

    [Tooltip("AIS range in meters")]
    public float aisRange = 10000f;

    [Tooltip("AIS position error in meters")]
    public float aisPositionError = 10f;

    [Header("Radar Sensor Parameters")]
    [Tooltip("Radar update interval in seconds")]
    public float radarUpdateInterval = 4f;

    [Tooltip("Radar range in meters")]
    public float radarRange = 80000f; // 80 km is typical for ship radars

    [Tooltip("Radar position error in meters")]
    public float radarPositionError = 50f;

    [Tooltip("Radar azimuth coverage in degrees")]
    public float radarAzimuthCoverage = 360f;

    [Header("EOIR Sensor Parameters")]
    [Tooltip("EOIR update interval in seconds")]
    public float eoirUpdateInterval = 0.1f; // EOIR typically updates much faster than AIS or radar

    [Tooltip("EOIR detection range in meters")]
    public float eoirRange = 15000f; // 15 km is typical for EOIR sensors on aircraft

    [Tooltip("EOIR position error in meters")]
    public float eoirPositionError = 20f;

    [Tooltip("EOIR field of view in degrees")]
    public float eoirFOV = 30f;

    [Header("Simulation Controls")]
    [Tooltip("Enable or disable individual sensors")]
    public bool aisEnabled = true;
    public bool radarEnabled = true;
    public bool eoirEnabled = true;


    private float _aisTimer = 0f;
    private float _radarTimer = 0f;
    private float _eoirTimer = 0f;

    void Start()
    {
        if (trackManager == null)
        {
            Debug.LogError("SensorSimulator: Trackmanager not assigned!");
            return;
        }

        // Find all simulated ships in the scene
        FindShipsInScene();     
    }

    void Update()
    {
        if (trackManager == null) return;

        // Update timers
        _aisTimer += Time.deltaTime;
        _radarTimer += Time.deltaTime;
        _eoirTimer += Time.deltaTime;

        // AIS Sensor Simulation
        if (aisEnabled && _aisTimer >= aisUpdateInterval)
        {
            SimulateAISSensor();
            _aisTimer = 0f;
        }

        // Radar Sensor Simulation
        if (radarEnabled && _radarTimer >= radarUpdateInterval)
        {
            SimulateRadarSensor();
            _radarTimer = 0f;
        }

        // EOIR Sensor Simulation
        if (eoirEnabled && _eoirTimer >= eoirUpdateInterval)
        {
            SimulateEOIRSensor();
            _eoirTimer = 0f;
        }
    }

    /// <summary>
    /// Finds all SimulatedShip instances in the scene and populates the simulatedShips list.
    /// </summary>

    private void FindShipsInScene()
    {
        simulatedShips.Clear();
        simulatedShips.AddRange(FindObjectsOfType<SimulatedShip>());
        Debug.Log("SensorSimulator: Found " + simulatedShips.Count + " simulated ships in scene.");
    }

    /// <summary>
    /// Sensor Simulation Methods
    /// </summary>
    
    private void SimulateAISSensor()
    {
        foreach (SimulatedShip ship in simulatedShips)
        {
            if (ship == null || !ship.gameObject.activeInHierarchy) continue;

            // Check if ship has ais transponder enabled
            if (!ship.aisTransponder) continue;

            float distanceToShip = Vector3.Distance(aircraftTransform.position, ship.transform.position);

            // Check if within AIS range
            if (distanceToShip <= aisRange) continue;

            // Create Ship data object with error applied@
            Ship aisShipData = CreateShipData(ship, aisPositionError);

            // Add or update track in TrackManager
            // Make new AIS data object

            AISData aisData = new AISData
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                ships = new List<Ship> { aisShipData }
            };

            // Add or update track in TrackManager
            trackManager.ProcessAISData(aisData);

            // Log detection
            Debug.Log($"[AIS] Detected ship: {ship.shipName} at distance {distanceToShip:F1} m");
            
        }
    }

    private void SimulateRadarSensor()
    {
        foreach (SimulatedShip ship in simulatedShips)
        {
            if (ship == null || !ship.gameObject.activeInHierarchy) continue;

            float distanceToShip = Vector3.Distance(aircraftTransform.position, ship.transform.position);

            // Check if within radar range
            if (distanceToShip <= radarRange) continue;

            Vector3 toShip = ship.transform.position - aircraftTransform.position;

            // Check if within radar azimuth coverage
            float azimuth = Vector3.SignedAngle(aircraftTransform.forward, toShip, Vector3.up);
            if (MathF.Abs(azimuth) > radarAzimuthCoverage / 2f) continue;

            // Add radar position error
            Vector3 detectedPosition = ship.transform.position + GetRandomPositionError(radarPositionError);

            // Calculate velocity
            Vector3 velocity = ship.GetVelocity();

            // Add or update track in TrackManager
            trackManager.ProcessRadarDetection(detectedPosition, velocity, DateTime.UtcNow);

            // Log detection
            Debug.Log($"[Radar] Detected ship: {ship.shipName} at distance {distanceToShip:F1} m, azimuth {azimuth:F1}°");
        }
    }

    private void SimulateEOIRSensor()
    {
        if (eoirCamera == null) return;

        foreach (ShimulatedShip ship in simulatedShips)
        {
            if (ship == null || !ship.gameObject.activeInHierarchy) continue;

             float distanceToShip = Vector3.Distance(aircraftTransform.position, ship.transform.position);

            // Check if within EOIR range
            if (distanceToShip <= eoirRange) continue;

            Vector3 toShip = ship.transform.position - aircraftTransform.position;

            // Check if within EOIR field of view
            Vector3 viewPortPoint = eoirCamera.WorldToViewportPoint(ship.transform.position);
            bool inView = viewPortPoint.z > 0 && viewPortPoint.x >= 0 && viewPortPoint.x <= 1 && viewPortPoint.y >= 0 && viewPortPoint.y <= 1;
            if (!inView) continue;

            // Add EOIR position error
            float rangeError = eoirPositionError * (distanceToShip / eoirRange); // Error increases with distance
            Vector3 detectedPosition = ship.transform.position + GetRandomPositionError(rangeError);

            // Add or update track in TrackManager
            trackManager.ProcessEOIRDetection(detectedPosition, DateTime.UtcNow);

            // Log detection
            Debug.Log($"[EOIR] Detected ship: {ship.shipName} at distance {distanceToShip:F1} m");

        }
    }

    /// <summary>
    /// Helper Methods
    /// </summary>
    
    private Ship CreateShipData(SimulatedShip ship, float positionError)
    {
        // Convert Unity world position to lat/lon
        Vector3 errorPosition = ship.transform.position + GetRandomPositionError(positionError);
        Vector2 latLon = WorldToLatLon(errorPosition);

        // Get velocity and course
        Vector3 velocity = ship.GetVelocity();
        float course = ship.GetCourse();
        float speed = velocity.magnitude * 1.94384f; // Convert from m/s to knots

        return new Ship
        {
            name = ship.shipName,
            mmsi = ship.mmsi,
            imo = ship.imo,
            lat = latLon.x,
            lon = latLon.y,
            course = course,
            speed = speed
        };
    }

    private Vector3 GetRandomPositionError(float magnitude)
    {
        float errorX = UnityEngine.Random.Range(-magnitude, magnitude);
        float errorZ = UnityEngine.Random.Range(-magnitude, magnitude);
        return new Vector3(errorX, 0f, errorZ);
    }

    private Vector2 WorldToLatLon(Vector3 worldPos)
    {
        // Need to convert from Unity's world coordinates to lat/lon.
    }

    /// <summary>
    /// Sensor Detection Visualization using Gizmos
    /// </summary>

    void OnDrawGizmos()
    {
        if (aircraftTransform == null) return;

        // Draw sensor ranges
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        if (aisEnabled) Gizmos.DrawWireSphere(aircraftTransform.position, aisRange);
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        if (radarEnabled) Gizmos.DrawWireSphere(aircraftTransform.position, radarRange);
        
        if (eoirEnabled && eoirCamera != null)
        {
            Gizmos.color = new Color(1, 1, 0, 0.1f);
            Gizmos.DrawFrustum(aircraftTransform.position, eoirFOV, eoirRange, 0.1f, 1f);
        }
    } 


}
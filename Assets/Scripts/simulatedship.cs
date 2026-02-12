using UnityEngine;
using Data;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

[RequireComponent(typeof(Rigidbody))]
public class SimulatedShip : MonoBehaviour
{
    [Header("Ship Identity")]
    public string shipName = "MV Simulated";
    public string mmsi = "123456789";

    public string imo = "IMO1234567";

    [Header("Ship Type")]
    public ShipType shipType = ShipType.Cargo;

    [Header("Sensor Visibility")]
    [Tooltip("Does the ship broadcast AIS?")]
    public bool aisTransponder = true;

    [Tooltip("Radar cross-section multiplier (for radar visibility)")]
    [Range(0.1f, 10f)]
    public float radarCrossSection = 1f;

    [Header("Movement Parameters")]
    public bool enableAutopilot = true;
    public float autopilotSpeed = 5f; // Speed in m/s
    public float autopilotCourse = 0f; // Course in degrees (0 = North, 90 = East)

    [Header("Visual Representation")]
    public Color shipColor = Color.Gray;

    private Rigidbody _rb;
    private MeshRenderer _renderer;
    private Vector3 _lastPosition;
    private float _lastUpdateTime;

    private enum ShipType
    {
        Cargo,
        Tanker,
        Passenger,
        Fishing,
        Military,
        Other
    }

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _renderer = GetComponent<MeshRenderer>();

        // Set initial color
        if (_renderer != null)
        {
            _renderer.material.color = shipColor;
        }
        // Initialize last position and time for movement calculations
        _lastPosition = transform.position;
        _lastUpdateTime = Time.time;

        // Generate random MMSI if not set
        if (string.IsNullOrEmpty(mmsi) || mmsi == "123456789")
        {
            mmsi = GenerateRandomMMSI();
        }

        // Generate random IMO if not set
        if (string.IsNullOrEmpty(imo) || imo == "IMO1234567")
        {
            imo = GenerateRandomIMO();
        }

        Debug.Log("Simulated Ship Initialized: " + shipName + " | MMSI: " + mmsi + " | IMO: " + imo);
    }

    void Update()
    {
        if (enableAutopilot && _rb != null)
        {
            // Simple autopilot logic: move forward at a constant speed and maintain course
            float courseRad = autopilotCourse * 180f / Mathf.PI; // Convert degrees to radians
            Vector3 direction = new Vector3(Mathf.Sin(courseRad), 0, Mathf.Cos(courseRad));

            _rb.velocity = direction * autopilotSpeed;

            // Rotate the ship to face the direction of movement
            if (direction != Vector3.Zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 2f);
            }
        }
    }

    public Vector3 GetVelocity()
    {
        if (_rb != null)
        {
            return _rb.velocity;
        }
        else
        {
            //Fallback to calculating velocity based on position change
            float deltaTime = Time.time - _lastUpdateTime;
            if (deltaTime > 0)
            {
                Vector3 velocity = (transform.position - _lastPosition) / deltaTime;
                _lastPosition = transform.position;
                _lastUpdateTime = Time.time;
                return velocity;
            }
            else
            {
                return Vector3.zero;
            }
        }
    }

    public float GetCourse()
    {
        Vector3 velocity = GetVelocity();
        if (velocity.magnitude < 0.1f) return 0f;

        float courseRad = MathF.Atan2(velocity.x, velocity.z) * 180f / Mathf.PI; // Convert radians to degrees
        if (courseRad < 0) courseRad += 360f; // Ensure course is between 0 and 360
        return courseRad;
    }

    public float GetSpeedInKnots()
    {
        return GetVelocity().magnitude * 1.94384f; // Convert m/s to knots
    }

    public Ship ToShipData()
    {
        Vector3 pos = transform.position;
        float lat = pos.Z / 110540f;
        float lon = pos.X / 111320f;

        return new Ship
        {
            name = shipName,
            mmsi = mmsi,
            imo = imo,
            lat = lat,
            lon = lon,
            course = GetCourse(),
            speed = GetSpeedInKnots()
        };
    }

    private string GenerateRandomMMSI()
    {
        // MMSI is a 9-digit number where the first digit is between 2 and 7
        int firstDigit = Random.Range(2, 8);
        int remainingDigits = Random.Range(10000000, 99999999);
        return firstDigit.ToString() + remainingDigits.Substring(0, 8);
    }

    private string GenerateRandomIMO()
    {
        int number = Random.Range(1000000, 9999999);
        return $"IMO{number}";
    }

    /// <summary>
    /// Visualize the ship in Scene view with Gizmos
    /// </summary>

    void OnDrawGizmos()
    {
        // Draw ship direction
        Gizmos.color = Color.Blue;
        Vector3 forward = transform.forward * 20f;
        Gizmos.DrawRay(transform.position, forward);

        // Draw velocity vector
        Vector3 velocity = GetVelocity();
        if (velocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.Green;
            Gizmos.DrawRay(transform.position, velocity * 10f);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw AIS range if transponder is enabled
        if (aisTransponder)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, aisRange);
        }
    }     
}

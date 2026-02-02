using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;

namespace Data
{
    public class TrackManager : MonoBehaviour
    {
        private Dictionary <string, Track> activeTracks = new Dictionary<string , Track>();

        //Track correlation data
        [Header("Correlation Settings")]
        [Tooltip("Maximum distance between two track points to be considered the same track (in meters)")]
        public float correlationDistanceThreshold = 500f; //500 meters

        [Tooltip("Maximum time difference between two track points to be considered the same track (in seconds)")]
        public float correlationTimeThreshold = 30f; //30 seconds

        [Header("Track Settings")]
        [Tooltip("Time in seconds after which a track is considered inactive if no new data is received")]
        public float trackInactivityTimeout = 120f; //2 minutes

        [Tooltip("Minimum number of observations required to consider a track valid")]
        public int minObservationsForValidTrack = 3;
        // Track observation count for validation
        private Dictionary<string, int> trackObservationCount = new Dictionary<string, int>();

        // Events for UI updates
        public event Action<Track> OnTrackCreated;
        public event Action<Track> OnTrackUpdated;
        public event Action<Track> OnTrackRemoved;

        // Reference to overlay system
        public AISOverlay overlaySystem;

        void Start()
        {
            // Start periodic cleanup of inactive tracks
            InvokeRepeating(nameof(RemoveInactiveTracks), 10f, 10f); //remove inactive tracks every 10 seconds
        }

        /// <summary>
        /// Processing Functions
        /// </summary>

        public void ProcessAISData(AISData aisData)
        {
            if (aisData == null || aisData.ships.Count == 0)
                return;

            foreach (Ship ship in aisData.ships)
            {
                ProcessShipData(ship);
            }

        }

        public void ProcessShipData(Ship ship)
        {
            // Make a track ID based on MMSI
            string trackId = $"AIS_{ship.MMSI}";

            // Convert latitude and longitude to Unity world position
            Vector3 position = GeoToWorldPosition(ship.lat, ship.lon);

            // Calculate velocity vector from speed and course
            Vector3 velocity = CalculateVelocityVector(ship.speed, ship.course);

            if (activeTracks.ContainsKey(trackId))
            {
                UpdateExistingTrack(trackId, position, velocity, sensorType: SensorType.AIS, ship);
            }
            else
            {
               // Try to find a correlated track first
               Track correlatedTrack = FindCorrelatedTrack(position, newSensorType: SensorType.AIS);
               if (correlatedTrack != null)
                {
                    string oldTrackId = correlatedTrack.trackid;
                    activeTracks.Remove(oldTrackId); // Remove old track because we will update it with new ID
                    trackObservationCount.Remove(oldTrackId); // Remove old observation count as well

                    correlatedTrack.trackid = trackId; // Update track ID to new AIS-based ID
                    activeTracks[trackId] = correlatedTrack; // Add updated track back to active tracks
                    trackObservationCount[trackId] = trackObservationCount.ContainsKey(oldTrackId) ? trackObservationCount[oldTrackId] : 1;

                    MergeTracks(correlatedTrack, position, velocity, sensorType: SensorType.AIS, ship);
                }
                else
                {
                    CreateNewTrack(trackId, position, velocity, sensorType: SensorType.AIS, ship);
                }
            }  
        }

        public void ProcessRadarDetection(Vector3 position, Vector3 velocity, DateTime timestamp)
        {
            //Try to find a correlated track first
            Track correlatedTrack = FindCorrelatedTrack(position, newSensorType: SensorType.Radar);

            if (correlatedTrack != null)
            {
                // Merge with existing track
                MergeTracks(correlatedTrack, position, velocity, sensorType: SensorType.Radar, shipData: null);
            }
            else
            {
                // Create a new track with an unique ID
                string trackId = $"Radar_{Guid.NewGuid().ToString().Substring(0, 8)}";
                CreateNewTrack(trackId, position, velocity, sensorType: SensorType.Radar, shipData: null);
            }
        }

        public void ProcessEOIRDetection(Vector3 position, DateTime timestamp)
        {
            Vector3 velocity = Vector3.Zero; // EO/IR may not provide velocity info

            //Try to find a correlated track first

            Track correlatedTrack = FindCorrelatedTrack(position, newSensorType: SensorType.EOIR);

            if (correlatedTrack != null)
            {
                // Merge with existing track
                MergeTracks(correlatedTrack, position, velocity, sensorType: SensorType.EOIR, shipData: null);
            }
            else
            {
                // Create a new track with an unique ID
                string trackId = $"EOIR_{Guid.NewGuid().ToString().Substring(0, 8)}";
                CreateNewTrack(trackId, position, velocity, sensorType: SensorType.EOIR, shipData: null);
            }
        }

        /// <summary>
        /// Track Management Functions
        /// </summary>
        /// <param name="trackId"></param>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        /// <param name="sensorType"></param>
        /// <param name="shipData"></param>

        private void CreateNewTrack(string trackId, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            
        }

        private void UpdateExistingTrack(string trackId, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            
        }

        private Track FindCorrelatedTrack(Vector3 position, SensorType newSensorType)
        {
            
        }

        private void MergeTracks(Track track, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            // Going to use Kalman filtering for merging track data from multiple sensors in the future
        }

        public void RemoveInactiveTracks()
        {
            
        }

        public void PredictTrackPositions(float deltaTime)
        {
            // Need to implement a prediction algorithm (e.g., Kalman filter) to estimate future positions based on current velocity and heading
        }

        public void PrintActiveTracks()
        {
            
        }

        public void ClearAllTracks()
        {
            
        }

        /// <summary>
        /// Track Helper Functions
        /// </summary>
        /// <returns></returns>

        public List<Track> GetActiveTracks()
        {
            return activeTracks.Values.ToList();
        }

        public List<Track> GetTracksBySensorType(SensorType sensorType)
        {
            return activeTracks.Values.Where(t=> t.sources.hasSensor(sensorType)).ToList();
        }

        public List<Track> GetConfirmedTracks()
        {
            return activeTracks.Values.Where(t => Track.state == TrackState.Confirmed).ToList();
        }

        public List<Track> GetTrackById(string trackId)
        {
            return activeTracks.ContainsKey(trackId) ? activeTracks[trackId] : null;
        }

       public List<Track> GetTrackByConfidence(IdentityConfidence minimumConfidence)
        {
            return activeTracks.Values.Where(t => t.identityConfidence >= minimumConfidence).ToList(); 
        }

        public int GetTrackCount()
        {
            return activeTracks.Count;
        }

        /// <summary>
        /// Utility Functions
        /// </summary>
        /// 
        private Vector3 GeoToWorldPosition(float latitude, float longitude)
        {
            // Placeholder function to convert geo-coordinates to Unity world position
            // Need to implement a homogenous transformation
        }

        private Vector3 CalculateVelocityVector(float course, float speed)
        {
            // Convert course from degrees to radians
            float courseRad = course * MathF.Deg2Rad;

            // Calculate velocity components
            float vx = speed * MathF.Sin(courseRad);
            float vz = speed * MathF.Cos(courseRad);

            return new Vector3(vx, 0, vz); // Assuming y=0 for sea level
        }
        
        
        


    }
}
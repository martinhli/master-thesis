using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Data;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

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

            foreach (Data.Ship ship in aisData.ships)
            {
                ProcessShipData(ship);
            }

        }

        public void ProcessShipData(Data.Ship ship)
        {
            // Make a track ID based on MMSI
            string trackId = $"AIS_{ship.mmsi}";

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
            Vector3 velocity = Vector3.zero; // EO/IR may not provide velocity info

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

        private void CreateNewTrack(string trackid, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            Track newTrack = new Track
            (
                trackId: trackid,
                position: position,
                velocity: velocity,
                sensorType: sensorType,
                shipData: shipData
            );

            newTrack.sources.addSensor(sensorType); // Mark the source sensor

            newTrack.identityConfidence = DetermineIdentityConfidence(newTrack);

            activeTracks[trackId] = newTrack;
            trackObservationCount[trackId] = 1; // First observation

            OnTrackCreated?.Invoke(newTrack); // Trigger event for UI update
        }

        private void UpdateExistingTrack(string trackId, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            Track track = activeTracks[trackId];

            track.position = position;
            track.velocity = velocity;
            track.timeStamp = DateTime.UtcNow;

            // Add sensor source if not already present
            if (!track.sources.hasSensor(sensorType))
            {
                track.sources.addSensor(sensorType);
            }

            // Update confidence
            Data.IdentityConfidence oldConfidence = track.identityConfidence;
            track.identityConfidence = DetermineIdentityConfidence(track);

            if (oldConfidence != track.identityConfidence)
            {
                // Confidence level changed, could trigger additional actions if needed
            }

            // Increment observation count
            if(trackObservationCount.ContainsKey(trackId))
            {
                trackObservationCount[trackId]++;

                // Confirm track if a threshold is reached
                if (trackObservationCount[trackId] >= confirmationThreshold &&
                    track.state != TrackState.Confirmed)
                {
                    track.state = TrackState.Confirmed;
                }
            }

            if (shipData != null)
            {
                track.shipData = shipData; // Update ship data if available
            }

            OnTrackUpdated?.Invoke(track); // Trigger event for UI update
        }

        private Track FindCorrelatedTrack(Vector3 position, SensorType newSensorType)
        {
            Track bestMatch = null;
            float minDistance = float.MaxValue;

            foreach (var entry in activeTracks)
            {
                Track track = entry.Value;

                // Skip if same sensor type
                if (track.sources.hasSensor(newSensorType))
                    continue;

                // Check time correlation
                TimeSpan timeDiff = DateTime.UtcNow - track.timeStamp;
                if (timeDiff.TotalSeconds > correlationDistanceThreshold)
                    continue;

                // Check spatial correlation
                float distance = Vector3.Distance(position, track.Position);
                if (distance < correlationDistanceThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    bestMatch = track;
                }
            }
            return bestMatch;
        }

        private void MergeTracks(Track track, Vector3 position, Vector3 velocity, SensorType sensorType, Ship shipData)
        {
            // Going to use Kalman filtering for merging track data from multiple sensors in the future
        }

        public void RemoveInactiveTracks()
        {
            List<string> tracksToRemove = new List<string>();

            foreach (var entry in activeTracks)
            {
                TimeSpan timeSinceUpdate = DateTime.UtcNow - entry.Value.timeStamp;
                if (timeSinceUpdate.TotalSeconds > trackInactivityTimeout)
                {
                    tracksToRemove.Add(entry.Key); 
                }
            }

            foreach (string trackId in tracksToRemove)
            {
                activeTracks.Remove(trackId);
                trackObservationCount.Remove(trackId);
                OnTrackRemoved?.Invoke(trackId); // Trigger event for UI update
            }
        }

        public void PredictTrackPositions(float deltaTime)
        {
            // Need to implement a prediction algorithm (e.g., Kalman filter) to estimate future positions based on current velocity and heading
        }

        public void PrintActiveTracks()
        {
            Debug.Log($"Active Tracks Count: {activeTracks.Count}");
            foreach (var entry in activeTracks)
            {
                Track track = entry.Value;
                string shipInfo = track.shipData != null ? track.shipData.name : "Unknown";
                Debug.Log($"Track ID: {track.trackid}, Position: {track.position}, Velocity: {track.velocity}, Confidence: {track.identityConfidence}, Ship: {shipInfo}");
            }
            
        }

        public void ClearAllTracks()
        {
            foreach (var id in activeTracks.Keys.ToList())
            {
                OnTrackRemoved?.Invoke(id);
            }

            activeTracks.Clear();
            trackObservationCount.Clear();
            
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
            
            return activeTracks.Values.Where(t => t.state == TrackState.Confirmed).ToList();
        }

        public Track GetTrackById(string trackId)
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
        /// 
        private IdentityConfidence DetermineIdentityConfidence(Track track)
        {
            if (!track.hasMultipleSensors())
            {
                // Single sensor source
                if (track.sources.hasSensor(Data.SensorType.AIS))
                {
                    return Data.IdentityConfidence.Medium; // AIS alone provides medium confidence
                }
                else
                {
                    return Data.IdentityConfidence.Low; // Radar or EO/IR alone provides low confidence
                }
            }
            else
            {
                // Multiple sensor sources
                if (track.sources.hasSensor(Data.SensorType.AIS))
                {
                    return Data.IdentityConfidence.Strong; // AIS combined with other sensors provides strong confidence
                }
                else
                {
                    return Data.IdentityConfidence.High; // Radar + EO/IR provides high confidence
                }
            }
        }
        private Vector3 GeoToWorldPosition(float latitude, float longitude)
        {
            // Placeholder function to convert geo-coordinates to Unity world position
            // Need to implement a homogenous transformation
        }

        private Vector3 CalculateVelocityVector(float course, float speed)
        {
            // Convert course from degrees to radians
            float courseRad = course * MathF.PI / 180f;

            // Calculate velocity components
            float vx = speed * MathF.Sin(courseRad);
            float vz = speed * MathF.Cos(courseRad);

            return new Vector3(vx, 0, vz); // Assuming y=0 for sea level
        }
        
        
        


    }
}
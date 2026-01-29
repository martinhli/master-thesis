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

        // Reference to overlay system
        public AISOverlay overlaySystem;

        void Start()
        {
            // Start periodic cleanup of inactive tracks
            InvokeRepeating(nameof(RemoveInactiveTracks), 10f, 10f); //remove inactive tracks every 10 seconds
        }

        public void ProcessAISData(AISData aisData)
        {
            if (aisData == null || aisData.ships == null || aisData.ships.Count == 0)
                return;
 
            foreach (var ship in aisData.ships)
            {
                ProcessShipData(ship);
            }

        }

        public void ProcessShipData(Ship ship)
        {
            
        }

        public void ProcessRadarDetection(Vector3 position, Vector3 velocity, DateTime timestamp)
        {
            
        }

        public void ProcessEOIRDetection(Vector3 position, DateTime timestamp)
        {
            
        }


    }
}
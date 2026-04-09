using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using Data;

public class trackoverlayer : MonoBehaviour
{
    public TrackManager trackManager;
    public GameObject labelPrefab;
    public Camera overlayCamera;
    public float shipAssociationDistanceThreshold = 300f;

    private Dictionary<string, GameObject> activeLabels = new Dictionary<string, GameObject>();

    void Update()
    {
        if (trackManager == null) return;
        if (labelPrefab == null)
        {
            Debug.LogError("trackoverlayer: labelPrefab is not assigned.");
            return;
        }

        // Get current confirmed tracks from the track manager
        var confirmedTracks = trackManager.GetConfirmedTracks();
        if (confirmedTracks == null) return;

        // Update existing labels and create new ones for new tracks
        HashSet<string> currentLabelKeys = new HashSet<string>();

        foreach (var track in confirmedTracks)
        {
            if (track == null || string.IsNullOrEmpty(track.trackid)) continue;

            string labelKey = GetStableLabelKey(track);
            currentLabelKeys.Add(labelKey);

            if (!activeLabels.ContainsKey(labelKey))
            {
                // Create new label for this track
                GameObject labelObj = Instantiate(labelPrefab);
                var label = labelObj.GetComponent<tracklabeler>();
                if (label == null)
                {
                    Debug.LogError("trackoverlayer: labelPrefab is missing tracklabeler component.");
                    Destroy(labelObj);
                    continue;
                }
                label.track = track;
                label.SetViewCamera(overlayCamera);
                activeLabels[labelKey] = labelObj;
            }
            else
            {
                if (activeLabels[labelKey] == null)
                {
                    activeLabels.Remove(labelKey);
                    continue;
                }

                // Update existing label
                var label = activeLabels[labelKey].GetComponent<tracklabeler>();
                if (label == null)
                {
                    Debug.LogError("trackoverlayer: existing label is missing tracklabeler component.");
                    Destroy(activeLabels[labelKey]);
                    activeLabels.Remove(labelKey);
                    continue;
                }
                label.track = track;
                label.SetViewCamera(overlayCamera);
            }
        }

        // Remove labels for tracks that are no longer confirmed
        List<string> tracksToRemove = new List<string>();
        foreach (var entry in activeLabels)
        {
            if (!currentLabelKeys.Contains(entry.Key))
            {
                Destroy(entry.Value);
                tracksToRemove.Add(entry.Key);
            }
        }
        foreach (var trackId in tracksToRemove)
        {
            activeLabels.Remove(trackId);
        }
    }

    private string GetStableLabelKey(Track track)
    {
        if (track.shipData != null)
        {
            if (!string.IsNullOrEmpty(track.shipData.mmsi))
            {
                return "MMSI_" + track.shipData.mmsi;
            }

            if (!string.IsNullOrEmpty(track.shipData.name))
            {
                return "NAME_" + track.shipData.name;
            }
        }

        SimulatedShip ship = FindBestShipForTrack(track);
        if (ship != null)
        {
            return "SHIP_" + ship.GetInstanceID();
        }

        return "TRACK_" + track.trackid;
    }

    private SimulatedShip FindBestShipForTrack(Track track)
    {
        if (track == null)
        {
            return null;
        }

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        SimulatedShip bestShip = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < ships.Length; i++)
        {
            SimulatedShip ship = ships[i];
            if (ship == null)
            {
                continue;
            }

            float distance = Vector3.Distance(track.position, ship.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestShip = ship;
            }
        }

        if (bestShip != null && bestDistance <= Mathf.Max(0f, shipAssociationDistanceThreshold))
        {
            return bestShip;
        }

        return null;
    }
}
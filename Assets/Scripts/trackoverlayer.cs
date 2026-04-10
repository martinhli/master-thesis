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
    public float shipAssociationDistanceThreshold = 1200f;
    public float maxLabelTimeoutSeconds = 1.5f;
    public float maxTrackAgeSeconds = 4f;
    public bool onlyShowUnknownTargetsInIdentityTaskModes = false;

    private Dictionary<string, GameObject> activeLabels = new Dictionary<string, GameObject>();
    private Dictionary<string, float> labelLastSeenTime = new Dictionary<string, float>();
    private UIManager uiManager;
    private EOIRCameraController eoirController;

    void Update()
    {
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }

        if (eoirController == null)
        {
            eoirController = FindFirstObjectByType<EOIRCameraController>();
        }

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

            if ((DateTime.UtcNow - track.timeStamp).TotalSeconds > Mathf.Max(0.1f, maxTrackAgeSeconds))
            {
                continue;
            }

            SimulatedShip associatedShip = FindBestShipForTrack(track);
            if (!ShouldRenderTrack(track, associatedShip))
            {
                continue;
            }

            string labelKey = GetStableLabelKey(track, associatedShip);
            currentLabelKeys.Add(labelKey);
            labelLastSeenTime[labelKey] = Time.time;

            bool isConfirmed = IsOverlayConfirmed(track, associatedShip);

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
                label.SetViewMainCamera(overlayCamera);
                label.SetViewEOIRCamera(eoirController != null ? eoirController.eoirCamera : null);
                label.SetConfirmedState(isConfirmed);
                activeLabels[labelKey] = labelObj;
            }
            else
            {
                if (activeLabels[labelKey] == null)
                {
                    activeLabels.Remove(labelKey);
                    labelLastSeenTime.Remove(labelKey);
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
                label.SetViewMainCamera(overlayCamera);
                label.SetViewEOIRCamera(eoirController != null ? eoirController.eoirCamera : null);
                label.SetConfirmedState(isConfirmed);
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

        float now = Time.time;
        foreach (var entry in activeLabels)
        {
            if (tracksToRemove.Contains(entry.Key))
            {
                continue;
            }

            if (!labelLastSeenTime.TryGetValue(entry.Key, out float lastSeen))
            {
                continue;
            }

            if (now - lastSeen > Mathf.Max(0.1f, maxLabelTimeoutSeconds))
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value);
                }
                tracksToRemove.Add(entry.Key);
            }
        }

        foreach (var trackId in tracksToRemove)
        {
            activeLabels.Remove(trackId);
            labelLastSeenTime.Remove(trackId);
        }
    }

    public void ClearAllLabels()
    {
        foreach (var label in activeLabels.Values)
        {
            if (label != null)
            {
                Destroy(label);
            }
        }

        activeLabels.Clear();
        labelLastSeenTime.Clear();
    }

    private bool ShouldRenderTrack(Track track, SimulatedShip associatedShip)
    {
        if (!onlyShowUnknownTargetsInIdentityTaskModes)
        {
            return true;
        }

        if (tracklabeler.ActiveDisplayMode != tracklabeler.LabelDisplayMode.RadarUncertainIdentity &&
            tracklabeler.ActiveDisplayMode != tracklabeler.LabelDisplayMode.FusedUncertaintyAwareIdentity)
        {
            return true;
        }

        // In identity-task scenarios, only render overlays for the unknown contacts.
        return associatedShip != null && !associatedShip.aisTransponder;
    }

    private bool IsOverlayConfirmed(Track track, SimulatedShip associatedShip)
    {
        if (uiManager == null)
        {
            return false;
        }

        if (uiManager.IsTrackConfirmed(track.trackid))
        {
            return true;
        }

        return uiManager.IsShipConfirmed(associatedShip);
    }

    private string GetStableLabelKey(Track track, SimulatedShip associatedShip)
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

        if (associatedShip != null)
        {
            return "SHIP_" + associatedShip.GetInstanceID();
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
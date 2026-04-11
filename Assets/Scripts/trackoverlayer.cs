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
    public bool hideConfirmedLabelsInIdentityScenarios = true;

    private Dictionary<string, GameObject> activeLabels = new Dictionary<string, GameObject>();
    private Dictionary<string, float> labelLastSeenTime = new Dictionary<string, float>();
    private HashSet<string> confirmedLabelKeys = new HashSet<string>();
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

        List<Track> tracksToRender = GetTracksToRender();
        if (tracksToRender == null) return;

        Dictionary<string, Track> bestTrackPerLabelKey = BuildRenderableTrackMap(tracksToRender);
        if (bestTrackPerLabelKey.Count == 0)
        {
            return;
        }

        // Update existing labels and create new ones for new tracks
        HashSet<string> currentLabelKeys = new HashSet<string>();

        foreach (var kvp in bestTrackPerLabelKey)
        {
            string labelKey = kvp.Key;
            Track track = kvp.Value;

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

            currentLabelKeys.Add(labelKey);
            labelLastSeenTime[labelKey] = Time.time;

            bool isConfirmedNow = IsOverlayConfirmed(track, associatedShip);
            if (isConfirmedNow)
            {
                confirmedLabelKeys.Add(labelKey);
            }
            bool isConfirmed = confirmedLabelKeys.Contains(labelKey);

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

        // Remove labels for tracks that are no longer part of current scenario track set
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
            confirmedLabelKeys.Remove(trackId);
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
        confirmedLabelKeys.Clear();
    }

    private List<Track> GetTracksToRender()
    {
        if (uiManager != null &&
            (uiManager.scenario == UIManager.StudyScenario.RadarEOIRDegraded ||
             uiManager.scenario == UIManager.StudyScenario.FusedUncertaintyAware))
        {
            return trackManager.GetActiveTracks();
        }

        return trackManager.GetConfirmedTracks();
    }

    private Dictionary<string, Track> BuildRenderableTrackMap(List<Track> tracks)
    {
        Dictionary<string, Track> result = new Dictionary<string, Track>();

        foreach (Track track in tracks)
        {
            if (track == null || string.IsNullOrEmpty(track.trackid))
            {
                continue;
            }

            SimulatedShip associatedShip = FindBestShipForTrack(track);
            if (!ShouldRenderTrack(track, associatedShip))
            {
                continue;
            }

            string key = GetStableLabelKey(track, associatedShip);
            if (!result.ContainsKey(key))
            {
                result[key] = track;
                continue;
            }

            Track existing = result[key];
            if (IsPreferredLabelTrack(track, existing))
            {
                result[key] = track;
            }
        }

        return result;
    }

    private bool IsPreferredLabelTrack(Track candidate, Track current)
    {
        if (candidate == null)
        {
            return false;
        }

        if (current == null)
        {
            return true;
        }

        int candidatePriority = GetDisplayIdPriority(candidate);
        int currentPriority = GetDisplayIdPriority(current);
        if (candidatePriority != currentPriority)
        {
            return candidatePriority > currentPriority;
        }

        // If priority is equal, prefer the freshest track for better spatial accuracy.
        return candidate.timeStamp > current.timeStamp;
    }

    private int GetDisplayIdPriority(Track track)
    {
        if (track == null || track.sources == null)
        {
            return 0;
        }

        // Prefer radar IDs in scene labels to match the contact list workflow.
        if (track.sources.hasSensor(SensorType.Radar))
        {
            return 3;
        }

        if (track.sources.hasSensor(SensorType.AIS))
        {
            return 2;
        }

        if (track.sources.hasSensor(SensorType.EOIR))
        {
            return 1;
        }

        return 0;
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
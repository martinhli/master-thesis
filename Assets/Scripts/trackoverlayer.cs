using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Data;

public class trackoverlayer : MonoBehaviour
{
    public TrackManager trackManager;
    public GameObject labelPrefab;

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
        HashSet<string> currentTrackIds = new HashSet<string>();

        foreach (var track in confirmedTracks)
        {
            if (track == null || string.IsNullOrEmpty(track.trackid)) continue;

            currentTrackIds.Add(track.trackid);

            if (!activeLabels.ContainsKey(track.trackid))
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
                activeLabels[track.trackid] = labelObj;
            }
            else
            {
                if (activeLabels[track.trackid] == null)
                {
                    activeLabels.Remove(track.trackid);
                    continue;
                }

                // Update existing label
                var label = activeLabels[track.trackid].GetComponent<tracklabeler>();
                if (label == null)
                {
                    Debug.LogError("trackoverlayer: existing label is missing tracklabeler component.");
                    Destroy(activeLabels[track.trackid]);
                    activeLabels.Remove(track.trackid);
                    continue;
                }
                label.track = track;
            }
        }

        // Remove labels for tracks that are no longer confirmed
        List<string> tracksToRemove = new List<string>();
        foreach (var entry in activeLabels)
        {
            if (!currentTrackIds.Contains(entry.Key))
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
}
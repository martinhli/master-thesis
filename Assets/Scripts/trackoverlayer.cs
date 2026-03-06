using UnityEngine;
using UnityEngine.UI;

public class trackoverlayer : MonoBehaviour
{
    public TrackManager trackManager;
    public GameObject labelPrefab;

    private Dictionary<string, GameObject> activeLabels = new Dictionary<string, GameObject>();

    void Update()
    {
        if (trackManager == null) return;

        // Get current confirmed tracks from the track manager
        var confirmedTracks = trackManager.GetConfirmedTracks();

        // Update existing labels and create new ones for new tracks
        HashSet<string> currentTrackIds = new HashSet<string>();

        foreach (var track in confirmedTracks)
        {
            currentTrackIds.Add(track.trackId);

            if (!activeLabels.ContainsKey(track.trackId))
            {
                // Create new label for this track
                GameObject labelObj = Instantiate(tracklabelPrefab);
                var label = labelObj.GetComponent<TrackLabel3D>();
                label.track = track;
                activeLabels[track.trackId] = labelObj;
            }
            else
            {
                // Update existing label
                var label = activeLabels[track.trackId].GetComponent<TrackLabel3D>();
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
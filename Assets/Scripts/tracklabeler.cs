using UnityEngine;
using UnityEngine.UI;
using TextMeshPro;
using System.Threading.Tasks.Dataflow;
using System.Numerics;
using System.Drawing;

public class tracklabeler : MonoBehaviour
{
    public TextMeshPro labelText;
    public Track track;

    void Update()
    {
        if (track != null) return;

        // Position label above the track
        transform.position = track.transform.position + Vector3.up * 10f;

        // Make label face the camera
        transform.lookAt(Camera.main.transform);
        transform.Rotate(0, 180f, 0);

        // Update label text with track info
    }

    void UpdateLabel()
    {
        if (track == null) return;

        string name = track.shipData != null ? track.shipData.name : "Unknown";
        string confidence = track.identityConfidence.ToString();
        labelText.text = name + "\n(" + confidence + ")";
        labelText.color = GetConfidenceColor(track.identityConfidence);
    }

    Color GetConfidenceColor (IdentityConfidence confidence)
    {
        switch (confidence)
        {
            case IdentityConfidence.Strong:
                return Color.cyan;
            case IdentityConfidence.High:
                return Color.green;
            case IdentityConfidence.Medium:
                return Color.yellow;
            case IdentityConfidence.Low:
                return Color.red;
            case IdentityConfidence.None:
                return Color.gray;
            default:
                return Color.white;
        }
    }
}
using UnityEngine;
using TMPro;
using Data;

public class tracklabeler : MonoBehaviour
{
    public TMP_Text labelText;
    public Track track;

    [Header("Label Placement")]
    public Camera viewCamera;
    public float fallbackLabelHeight = 20f;
<<<<<<< HEAD
    public float extraHeightAboveShip = 40f;
=======
    public float extraHeightAboveShip = 60f;
>>>>>>> origin/feature/vr-scene

    [Header("Distance Scaling")]
    [Tooltip("Distance at which the label appears at its original size.")]
    public float referenceDistance = 1000f;
    [Tooltip("Minimum scale.")]
    public float minScale = 0.5f;
    [Tooltip("Maximum scale.")]
    public float maxScale = 200f;

    private SimulatedShip _ship;
    private Renderer[] _shipRenderers;

    void Start()
    {
        // Force the TMP material to render on top of all scene geometry (including water shaders)
        // by setting ZTest to Always. We instance the material so other labels are unaffected.
        if (labelText != null)
        {
            labelText.fontMaterial = new Material(labelText.fontMaterial);
            labelText.fontMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            // Overlay render queue (4000+) ensures the label draws after all opaque/transparent passes,
            // so it is never occluded by water, fog, or any other scene geometry.
            labelText.fontMaterial.renderQueue = 4000;
        }
    }

    public void SetViewCamera(Camera cam)
    {
        viewCamera = cam;
    }

    void Update()
    {
        if (track == null) return;

        CacheShipReferenceIfNeeded();

        // Place the label above the actual ship model.
        if (_ship != null)
        {
            Vector3 shipPos = _ship.transform.position;
            float shipHeight = GetShipHeight();
            transform.position = shipPos + Vector3.up * (shipHeight + extraHeightAboveShip);
        }
        else
        {
            transform.position = track.position + Vector3.up * fallbackLabelHeight;
        }

        // Face the active aircraft view camera, not implicitly Camera.main.
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;

            // Scale label so it appears the same angular size regardless of distance.
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float scaleFactor = Mathf.Clamp(dist / referenceDistance, minScale, maxScale);
            transform.localScale = Vector3.one * scaleFactor;
        }

        // Update label text with track info
        UpdateLabel();
    }

    private void CacheShipReferenceIfNeeded()
    {
        if (_ship != null || track == null || track.shipData == null) return;
        if (string.IsNullOrEmpty(track.shipData.name)) return;

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        for (int i = 0; i < ships.Length; i++)
        {
            if (ships[i] != null && ships[i].shipName == track.shipData.name)
            {
                _ship = ships[i];
                _shipRenderers = _ship.GetComponentsInChildren<Renderer>();
                return;
            }
        }
    }

    private float GetShipHeight()
    {
        if (_shipRenderers == null || _shipRenderers.Length == 0)
        {
            return fallbackLabelHeight;
        }

        Bounds combined = _shipRenderers[0].bounds;
        for (int i = 1; i < _shipRenderers.Length; i++)
        {
            if (_shipRenderers[i] != null)
            {
                combined.Encapsulate(_shipRenderers[i].bounds);
            }
        }

        return combined.extents.y;
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
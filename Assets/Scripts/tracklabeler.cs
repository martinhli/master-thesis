using UnityEngine;
using TMPro;
using Data;

public class tracklabeler : MonoBehaviour
{
    public enum LabelDisplayMode
    {
        AISDeterministicIdentity = 1,
        RadarUncertainIdentity = 2,
        FusedUncertaintyAwareIdentity = 3
    }

    public static LabelDisplayMode ActiveDisplayMode = LabelDisplayMode.AISDeterministicIdentity;

    public TMP_Text labelText;
    public Track track;
    public bool isConfirmed;
    public Color confirmedLabelColor = Color.green;
    public string confirmedTag = "CONFIRMED";

    [Header("Label Placement")]
    public Camera mainViewCamera;
    public Camera eoirViewCamera;
    public float fallbackLabelHeight = 20f;
    public float extraHeightAboveShip = 60f;

    [Header("Dual-Camera Billboard")]
    [Tooltip("Blend orientation toward EO/IR when EO/IR is actively pointed at this overlay")]
    public bool enableEOIRBillboardBlend = true;

    [Tooltip("Minimum EO/IR forward dot product to consider camera pointing at this overlay")]
    [Range(0.7f, 1f)]
    public float eoirLookDotThreshold = 0.96f;

    [Tooltip("Viewport tolerance around center for EO/IR engagement")]
    [Range(0.01f, 0.5f)]
    public float eoirViewportCenterTolerance = 0.2f;

    [Tooltip("How much EO/IR affects billboard direction when engaged")]
    [Range(0f, 1f)]
    public float eoirBlendWeight = 0.5f;

    [Header("Orientation Offset")]
    [Tooltip("Euler rotation offset applied after billboard facing. Use X=180 to flip vertically.")]
    public Vector3 billboardRotation = new Vector3(0f, 180f, 0f);

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

    public void SetViewMainCamera(Camera cam)
    {
        mainViewCamera = cam;
    }

    public void SetViewEOIRCamera(Camera cam)
    {
        eoirViewCamera = cam;
    }

    public void SetConfirmedState(bool confirmed)
    {
        isConfirmed = confirmed;
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
        Camera cam = mainViewCamera != null ? mainViewCamera : Camera.main;
        if (cam != null)
        {
            Vector3 toMainCamera = (cam.transform.position - transform.position).normalized;
            Vector3 lookDirection = toMainCamera;

            if (enableEOIRBillboardBlend && ShouldBlendTowardEOIR())
            {
                Vector3 toEOIRCamera = (eoirViewCamera.transform.position - transform.position).normalized;
                float weight = Mathf.Clamp01(eoirBlendWeight);
                lookDirection = Vector3.Slerp(toMainCamera, toEOIRCamera, weight).normalized;
            }

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                transform.rotation *= Quaternion.Euler(billboardRotation);
            }

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

    private bool ShouldBlendTowardEOIR()
    {
        if (eoirViewCamera == null)
        {
            return false;
        }

        Vector3 toLabel = transform.position - eoirViewCamera.transform.position;
        if (toLabel.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float forwardDot = Vector3.Dot(eoirViewCamera.transform.forward, toLabel.normalized);
        if (forwardDot < eoirLookDotThreshold)
        {
            return false;
        }

        Vector3 vp = eoirViewCamera.WorldToViewportPoint(transform.position);
        if (vp.z <= 0f)
        {
            return false;
        }

        float dx = Mathf.Abs(vp.x - 0.5f);
        float dy = Mathf.Abs(vp.y - 0.5f);
        return dx <= eoirViewportCenterTolerance && dy <= eoirViewportCenterTolerance;
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

        if (labelText == null)
        {
            return;
        }

        switch (ActiveDisplayMode)
        {
            case LabelDisplayMode.AISDeterministicIdentity:
            {
                string identity = track.shipData != null && !string.IsNullOrEmpty(track.shipData.name)
                    ? track.shipData.name
                    : track.trackid;
                labelText.text = isConfirmed ? $"{identity}\\n[{confirmedTag}]" : identity;
                labelText.color = isConfirmed ? confirmedLabelColor : Color.white;
                break;
            }

            case LabelDisplayMode.RadarUncertainIdentity:
            {
                labelText.text = isConfirmed ? $"RADAR CONTACT\\n[{confirmedTag}]" : "RADAR CONTACT";
                labelText.color = isConfirmed ? confirmedLabelColor : Color.yellow;
                break;
            }

            case LabelDisplayMode.FusedUncertaintyAwareIdentity:
            {
                string identity = track.shipData != null && !string.IsNullOrEmpty(track.shipData.name)
                    ? track.shipData.name
                    : "UNKNOWN";
                string baseLabel = $"{identity}\\nConf: {track.identityConfidence}\\nU: {track.positionUncertainty:F0} m";
                labelText.text = isConfirmed ? $"{baseLabel}\\n[{confirmedTag}]" : baseLabel;
                labelText.color = isConfirmed ? confirmedLabelColor : GetConfidenceColor(track.identityConfidence);
                break;
            }
        }
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

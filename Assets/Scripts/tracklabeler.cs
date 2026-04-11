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
    public float extraHeightAboveShip = 80f;

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

    [Tooltip("Optional rotation after billboard facing.")]
    public Vector3 billboardRotation = new Vector3(0f, 180f, 0f);

    [Tooltip("Keep labels upright with runtime defaults.")]
    public bool forceUprightBillboard = true;

    [Tooltip("Maximum distance for associating a label track to a ship when explicit ship data is missing.")]
    public float shipAssociationDistanceThreshold = 1200f;

    [Header("Distance Scaling")]
    [Tooltip("Distance at which the label appears at its original size.")]
    public float referenceDistance = 1000f;
    [Tooltip("Minimum scale.")]
    public float minScale = 0.5f;
    [Tooltip("Maximum scale.")]
    public float maxScale = 200f;

    private SimulatedShip _ship;
    private Renderer[] _shipRenderers;
    private Quaternion _prefabLocalRotation = Quaternion.identity;

    void Start()
    {
        _prefabLocalRotation = transform.localRotation;

        if (labelText != null)
        {
            labelText.fontMaterial = new Material(labelText.fontMaterial);
            labelText.fontMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
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
        if (track == null)
        {
            return;
        }

        CacheShipReferenceIfNeeded();

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

        Camera cam = mainViewCamera != null ? mainViewCamera : Camera.main;
        if (cam != null)
        {
            Vector3 mainDir = cam.transform.position - transform.position;
            if (mainDir.sqrMagnitude < 0.0001f)
            {
                mainDir = cam.transform.forward;
            }

            Vector3 finalDir = mainDir.normalized;

            if (enableEOIRBillboardBlend && ShouldBlendTowardEOIR())
            {
                float weight = Mathf.Clamp01(eoirBlendWeight);
                Vector3 eoirDir = eoirViewCamera.transform.position - transform.position;
                if (eoirDir.sqrMagnitude > 0.0001f)
                {
                    finalDir = Vector3.Slerp(finalDir, eoirDir.normalized, weight).normalized;
                }
            }

            Quaternion targetRotation = forceUprightBillboard
                ? Quaternion.LookRotation(finalDir, Vector3.up)
                : Quaternion.LookRotation(finalDir, cam.transform.up);

            Quaternion runtimeOffset = forceUprightBillboard
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.Euler(billboardRotation);

            transform.rotation = targetRotation * _prefabLocalRotation * runtimeOffset;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float scaleFactor = Mathf.Clamp(dist / referenceDistance, minScale, maxScale);
            transform.localScale = Vector3.one * scaleFactor;
        }

        UpdateLabel();
    }

    private void CacheShipReferenceIfNeeded()
    {
        if (_ship != null || track == null)
        {
            return;
        }

        if (track.shipData != null && !string.IsNullOrEmpty(track.shipData.name))
        {
            SimulatedShip[] namedShips = FindObjectsOfType<SimulatedShip>();
            for (int i = 0; i < namedShips.Length; i++)
            {
                if (namedShips[i] != null && namedShips[i].shipName == track.shipData.name)
                {
                    _ship = namedShips[i];
                    _shipRenderers = _ship.GetComponentsInChildren<Renderer>();
                    return;
                }
            }
        }

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        SimulatedShip nearest = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < ships.Length; i++)
        {
            if (ships[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(track.position, ships[i].transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = ships[i];
            }
        }

        if (nearest != null && bestDistance <= Mathf.Max(0f, shipAssociationDistanceThreshold))
        {
            _ship = nearest;
            _shipRenderers = _ship.GetComponentsInChildren<Renderer>();
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

    private void UpdateLabel()
    {
        if (labelText == null)
        {
            return;
        }

        switch (ActiveDisplayMode)
        {
            case LabelDisplayMode.AISDeterministicIdentity:
            {
                string identity = GetContactListID();
                labelText.text = isConfirmed ? $"{identity}\n[{confirmedTag}]" : identity;
                labelText.color = isConfirmed ? confirmedLabelColor : Color.white;
                break;
            }

            case LabelDisplayMode.RadarUncertainIdentity:
            {
                string radarIdentity = GetContactListID();
                labelText.text = isConfirmed ? $"{radarIdentity}\n[{confirmedTag}]" : radarIdentity;
                labelText.color = isConfirmed ? confirmedLabelColor : Color.yellow;
                break;
            }

            case LabelDisplayMode.FusedUncertaintyAwareIdentity:
            {
                string identity = GetContactListID();
                string baseLabel = $"{identity}\nConf: {track.identityConfidence}\nU: {track.positionUncertainty:F0} m";
                labelText.text = isConfirmed ? $"{baseLabel}\n[{confirmedTag}]" : baseLabel;
                labelText.color = isConfirmed ? confirmedLabelColor : GetConfidenceColor(track.identityConfidence);
                break;
            }
        }
    }

    private string GetContactListID()
    {
        return $"{GetRadarId()}\n{GetKnownUnknownTag()}";
    }

    private string GetRadarId()
    {
        if (track != null && !string.IsNullOrEmpty(track.trackid))
        {
            return track.trackid;
        }

        return "RADAR_UNKNOWN";
    }

    private string GetKnownUnknownTag()
    {
        bool isKnown = _ship != null ? _ship.aisTransponder : (track != null && track.shipData != null && !string.IsNullOrEmpty(track.shipData.name));
        return isKnown ? "KNOWN" : "UNKNOWN";
    }

    private Color GetConfidenceColor(IdentityConfidence confidence)
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

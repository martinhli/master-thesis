using UnityEngine;
using UnityEngine.UI;

public class EOIRMinimap : MonoBehaviour
{
    public EOIRFootprintTracker tracker;
    public RawImage minimapImage;

    [Tooltip("Optional ownship heading icon on top of minimap")]
    public RectTransform headingIcon;

    [Tooltip("Transform used to compute heading arrow rotation")]
    public Transform headingSource;

    void Update()
    {
        if (tracker != null && minimapImage != null && tracker.CoverageTexture != null)
        {
            if (minimapImage.texture != tracker.CoverageTexture)
            {
                minimapImage.texture = tracker.CoverageTexture;
            }
        }

        UpdateHeadingIcon();
    }

    void UpdateHeadingIcon()
    {
        if (headingIcon == null || headingSource == null)
        {
            return;
        }

        Vector3 planarForward = headingSource.forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        planarForward.Normalize();
        float headingDeg = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
        headingIcon.localRotation = Quaternion.Euler(0f, 0f, -headingDeg);
    }
}

using UnityEngine;

public class FollowARImage : MonoBehaviour
{
    public Transform target;   // the ARTrackedImage.transform
    public Vector3 localOffset = Vector3.zero;  // optional small lift
    public bool copyScale = false;              // ARTrackedImage scale is usually 1

    void LateUpdate()
    {
        if (!target) return;
        transform.position = target.position;
        transform.rotation = target.rotation;
        if (copyScale) transform.localScale = target.lossyScale;
        // A tiny lift to avoid z-fighting, if you want:
        if (localOffset != Vector3.zero)
            transform.position += transform.rotation * localOffset;
    }
}


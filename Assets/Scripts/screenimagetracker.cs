using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ScreenImageTracker : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager;
    public GameObject screenAnchorPrefab;
    public ARInitializationManager arManager;

    void OnEnable()
    {
        if (!trackedImageManager) trackedImageManager = FindObjectOfType<ARTrackedImageManager>();
        var lib = trackedImageManager ? trackedImageManager.referenceLibrary : null;
        Debug.Log($"[ImageTracker] Enabled. LibraryCount={(lib!=null ? lib.count : 0)}");
        if (trackedImageManager) trackedImageManager.trackedImagesChanged += OnChanged;
    }

    void OnDisable()
    {
        if (trackedImageManager) trackedImageManager.trackedImagesChanged -= OnChanged;
    }

    void OnChanged(ARTrackedImagesChangedEventArgs args)
    {
        Debug.Log($"[ImageTracker] added={args.added.Count} updated={args.updated.Count} removed={args.removed.Count}");
        foreach (var img in args.added)   Handle(img);
        foreach (var img in args.updated) Handle(img);
    }

    public Transform worldAnchorsRoot;

    void Handle(ARTrackedImage img)
    {
        Debug.Log($"[ImageTracker] seen name='{img.referenceImage.name}' state={img.trackingState} size={img.size}");
        if (img.trackingState == TrackingState.None) return; // ignore completely lost

        // Find existing by image id/name, or create it
        var existing = worldAnchorsRoot ? worldAnchorsRoot.Find($"ScreenAnchor_{img.trackableId}") : null;
        if (!existing)
        {
            var anchor = Instantiate(screenAnchorPrefab);
            anchor.name = $"ScreenAnchor_{img.trackableId}";
            anchor.transform.SetParent(worldAnchorsRoot, false);

            // Follow the ARTrackedImage pose every frame
            var follower = anchor.AddComponent<FollowARImage>();
            follower.target = img.transform;
            follower.localOffset = Vector3.zero; // The UI gets lifted up from the screen

                
            Debug.Log("[ImageTracker] Anchor created under tracked image.");
            arManager.OnScreenAnchorDetected(anchor);

        }
        else
        {
            existing.gameObject.SetActive(true);
        }
    }
}




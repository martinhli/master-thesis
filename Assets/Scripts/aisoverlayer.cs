using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Data;

using UnityEngine;

public class AISOverlay : MonoBehaviour
{
    [Header("Refs")]
    public Camera eoCamera;
    public RectTransform screenTransform;
    public Transform videoQuad;
    public int videoWidth = 1920;
    public int videoHeight = 1080;

    [Header("Visual")]
    public GameObject annotationVisualPrefab;     // assign AIS_AnnotationVisual

    private readonly List<GameObject> spawned = new();

    public void ClearLabels()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }

    public void OverlayShips(List<Ship> ships, CInterop.Point2D[] screenPoints)
    {
        
        ClearLabels();
        if (eoCamera == null) {
            Debug.LogError("AISOverlay: eoCamera not assigned");
            return; 
        }
        if (videoQuad == null) {
            Debug.LogError("AISOverlay: videoQuad not assigned");
            return; 
        }
        if (ships == null) {
            Debug.LogWarning("AISOverlay: ships list is null");
            return;
        }

        if (screenPoints == null) {
            Debug.LogWarning("AISOverlay: screenpoints array is null");
            return;
        }

        if (annotationVisualPrefab == null) {
            Debug.LogError("AISOverlay: annotationVisualPrefab is not assigned");
            return;
        }

        if (screenTransform == null) {
            Debug.LogError("AISOverlay: screenTransform is not assigned");
            return;
        }
        int count = Mathf.Min(ships.Count, screenPoints.Length);
        if (count == 0) {
            Debug.LogWarning("AISOverlay: no ships or screenPoints to overlay.");
            return;
        }

        Debug.Log($"AISOverlay: OverlayShips receiving {count} points.");

        if (screenPoints.Length > 0)
        {
            Debug.Log($"OverlayShips receiving {screenPoints.Length} points. " +
                    $"First point: {screenPoints[0].x}, {screenPoints[0].y}");
        }

        int validPointCount = 0;
        for (int i = 0; i < count; i++)
        {
            var point = screenPoints[i];
            validPointCount++;

            var uiElement = Instantiate(annotationVisualPrefab, screenTransform);
            if (uiElement == null) {
                Debug.LogError("AISOverlay: Instantiate returned null - check prefab.");
                continue;
            }
            var rect = uiElement.GetComponent<RectTransform>();
            if (rect == null) {
                Debug.LogError("AISOverlay: Prefab has no RectTransform. It must be a UI object (e.g. under a Canvas).");
                Destroy(uiElement);
                continue;
            }
            var view = uiElement.GetComponent<ShipInfoHolder>();
            if (view == null)
            {
                Debug.LogError("AISOverlay: Prefab is missing ShipInfoHolder component.");
                Destroy(uiElement);
                continue;
            }

            float uNorm = point.x / (videoWidth-1f);
            float vNorm = point.y / (videoHeight-1f);

            vNorm = 1f - vNorm;

            Vector3 localOnQuad = new Vector3(uNorm - 0.5f, vNorm - 0.5f, 0f);
            Vector3 worldOnQuad = videoQuad.TransformPoint(localOnQuad);

            // If your projection treats (0,0) as bottom-left, keep as-is.
            // If it treats (0,0) as top-left, use:
            // screenPos = new Vector2(point.x, Screen.height - point.y);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(eoCamera, worldOnQuad);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screenTransform,      // the Canvas RectTransform
                screenPos,
                null,                 // camera is null for Screen Space Overlay
                out localPos
            );

            bool invalidPoint = (Mathf.Abs(point.x) > 9000f || Mathf.Abs(point.y) > 9000f);

            bool offScreen = screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

            if (invalidPoint || offScreen) {
                Debug.LogWarning(
                $"AISOverlay: skipping ship {i} ({ships[i].name}) – invalid or off-screen point ({point.x}, {point.y})");
                continue;
            }


            if (i == 0)
            {
                Debug.Log($"First ship UI pos (local): {localPos}");
            }

            // Debug so we can see where Unity thinks this is:
            Debug.Log($"AISOverlay: ship {i} screen=({point.x:F1},{point.y:F1}) → local=({localPos.x:F1},{localPos.y:F1})");

            rect.anchoredPosition = localPos;
            view.SetShipData(ships[i]);
            spawned.Add(uiElement);
        }
        Debug.Log($"AISOverlay: {validPointCount}/{count} projected points are valid and on-screen.");

        if (MetricsManager.Instance != null && MetricsManager.Instance.metricsEnabled)
        {
            // Convert CInterop.Point2D[] -> UnityEngine.Vector2[]
            var pts = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                pts[i] = new Vector2(screenPoints[i].x, screenPoints[i].y);
            }

            MetricsManager.Instance.OnOverlaysRendered(ships, pts);
        }
    }
}


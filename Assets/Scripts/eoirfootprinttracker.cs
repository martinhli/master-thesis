using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Numerics;

using Debug = UnityEngine.Debug;
using Color = UnityEngine.Color;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Plane = UnityEngine.Plane;
public class EOIRFootprintTracker : MonoBehaviour
{
    [Header("References")]
    public Camera eoirCamera;
    public Transform aircraftTransform;

    [Header("Footprint Settings")]
    public int maxFootprints = 50; // Maximum number of footprints to keep at once
    public float footprintInterval = 0.25f; // Time in seconds between footprint updates
    public float footprintLifetime = 300f; // Fade after this many seconds
    public float maxDistance = 50000f; // Max distance from aircraft to place footprints (in meters)
    public LayerMask footprintSurfaceMask = ~0; // Layers to consider for footprint placement (default to everything)
    public bool useViewportCoverage = true;  // If true, will try to cover the entire camera viewport with footprints. If false, will just raycast from center of camera.
    public bool projectOntoSeaPlane = true;
    public float seaSurface = 0f;

    [Header("Visualization Settings")]
    public Shader footprintShader; // Custom shader for footprint visualization (optional, can use default transparent shader)
    public Color recentColor = new Color(0, 1, 1, 0.8f); // Bright cyan
    public Color oldColor = new Color(0, 0.5f, 0.5f, 0.2f); // Faded cyan
    public float footprintSize = 100f; // Meters covered by each footprint
    public float heightOffset = 10f; // Height above sea level to place footprints
    public bool keepFootprintsInWorldSpace = true; 

    public bool fadeOverTime = false;

    [System.Serializable]
    public class Footprint
    {
        public Vector3 position;
        public float timestamp;
        public GameObject visualObject;
    }

    private Queue<Footprint> footprints = new Queue<Footprint>(); // Using queue to process fooprints in order they arrived
    private float lastFootprintTime;
    private Material footprintMaterial;

    void Start()
    {
        // Need to implement a custom shader that can handle fading over time, or use a simple material with color property we can modify
        CreateFootprintMaterial();
    }

    void Update()
    {
        // Add new footprints
        if (Time.time - lastFootprintTime >= footprintInterval)
        {
            AddFootprint();
            lastFootprintTime = Time.time;
        }

        // Update existing footprints
        UpdateFootprints();
    }

    void CreateFootprintMaterial()
    {
        // Create transparent material for footprints.
        Shader shader = footprintShader;
        if (shader == null)
        {
            shader = Shader.Find("Custom/EOIR_Circle_Footprint");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            Debug.LogError("[EOIRFootprintTracker] No valid footprint shader found.");
            return;
        }

        footprintMaterial = new Material(shader);
        footprintMaterial.color = recentColor;
        footprintMaterial.renderQueue = (int)RenderQueue.Transparent;

        if (footprintMaterial.HasProperty("_Surface"))
        {
            footprintMaterial.SetFloat("_Surface", 1f); // Transparent surface in URP
        }

        if (footprintMaterial.HasProperty("_BaseColor"))
        {
            footprintMaterial.SetColor("_BaseColor", recentColor);
        }

        if (footprintMaterial.HasProperty("_Color"))
        {
            footprintMaterial.SetColor("_Color", recentColor);
        }
    }

    void AddFootprint()
    {
        if (eoirCamera == null) return;

        int layerMask = footprintSurfaceMask.value == 0 ? ~0 : footprintSurfaceMask.value;

        Vector3 centerPoint = Vector3.zero;
        float sizeX = footprintSize;
        float sizeZ = footprintSize;

        bool hasCoverage = useViewportCoverage && TryGetViewportCoverage(layerMask, out centerPoint, out sizeX, out sizeZ);

        if (!hasCoverage)
        {
            // Fallback: center raycast from camera
            RaycastHit hit;
            if (!Physics.Raycast(
                eoirCamera.transform.position,
                eoirCamera.transform.forward,
                out hit,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore))
            {
                return;
            }

            centerPoint = hit.point;
            sizeX = footprintSize;
            sizeZ = footprintSize;
        }

        // Create new footprint at hit location
        Footprint footprint = new Footprint
        {
            position = centerPoint + Vector3.up * heightOffset,
            timestamp = Time.time,
            visualObject = CreateFootprintVisual(centerPoint + Vector3.up * heightOffset, sizeX, sizeZ)
        };
        footprints.Enqueue(footprint);

        // Remove old footprints
        while (footprints.Count > maxFootprints)
        {
            RemoveFootprint();
        }
    }

    bool TryGetViewportCoverage(int layerMask, out Vector3 center, out float sizeX, out float sizeZ)
    {
        Vector3[] viewportCorners =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f)
        };

        Vector3[] hits = new Vector3[4];
        for (int i = 0; i < viewportCorners.Length; i++)
        {
            Ray ray = eoirCamera.ViewportPointToRay(viewportCorners[i]);
            if (!TryGetCoveragePoint(ray, layerMask, out hits[i]))
            {
                center = Vector3.zero;
                sizeX = footprintSize;
                sizeZ = footprintSize;
                return false;
            }
        }

        center = (hits[0] + hits[1] + hits[2] + hits[3]) * 0.25f;
        float bottomWidth = Vector3.Distance(hits[0], hits[1]);
        float topWidth = Vector3.Distance(hits[2], hits[3]);
        float leftHeight = Vector3.Distance(hits[0], hits[2]);
        float rightHeight = Vector3.Distance(hits[1], hits[3]);

        sizeX = Mathf.Max(1f, (bottomWidth + topWidth) * 0.5f);
        sizeZ = Mathf.Max(1f, (leftHeight + rightHeight) * 0.5f);
        return true;
    }

    bool TryGetCoveragePoint(Ray ray, int layerMask, out Vector3 point)
    {
        if (projectOntoSeaPlane)
        {
            Plane seaPlane = new Plane(Vector3.up, new Vector3(0f, seaSurface, 0f));
            if (seaPlane.Raycast(ray, out float enter) && enter >= 0f && enter <= maxDistance)
            {
                point = ray.GetPoint(enter);
                return true;
            }
        }

        return TryGetFarthestHit(ray, layerMask, out point);
    }

    bool TryGetFarthestHit(Ray ray, int layerMask, out Vector3 hitPoint)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            hitPoint = Vector3.zero;
            return false;
        }

        float farthestDistance = -1f;
        Vector3 farthestPoint = Vector3.zero;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].distance > farthestDistance)
            {
                farthestDistance = hits[i].distance;
                farthestPoint = hits[i].point;
            }
        }

        hitPoint = farthestPoint;
        return true;
    }

    void RemoveFootprint()
    {
        if (footprints.Count > 0)
        {
            Footprint oldestFootprint = footprints.Dequeue();
            if (oldestFootprint.visualObject != null)
            {
                Destroy(oldestFootprint.visualObject);
            }
        }
    }

    void UpdateFootprints()
    {
        for (int i = footprints.Count - 1; i >= 0; i--)
        {
            Footprint footprint = footprints.ToArray()[i];;
            float age = Time.time - footprint.timestamp;

            // Remove if too old
            if (fadeOverTime && age >= footprintLifetime)
            {
                RemoveFootprint();
                continue;
            }

            // Update visual appearance based on age
            if (footprint.visualObject != null)
            {
                float fadePercent = fadeOverTime ? age / footprintLifetime : 0f; // 0 = recentColor, 1 = oldColor
                Color currentColor = Color.Lerp(recentColor, oldColor, Mathf.Clamp01(fadePercent));

                // Update all renderers in case footprint prefab has multiple meshes.
                MeshRenderer[] renderers = footprint.visualObject.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in renderers)
                {
                    renderer.material.color = currentColor;

                    if (renderer.material.HasProperty("_BaseColor"))
                    {
                        renderer.material.SetColor("_BaseColor", currentColor);
                    }

                    if (renderer.material.HasProperty("_Color"))
                    {
                        renderer.material.SetColor("_Color", currentColor);
                    }
                }
            }
        }
    }

    GameObject CreateFootprintVisual(Vector3 position, float sizeX, float sizeZ)
    {
        if (footprintMaterial == null)
        {
            CreateFootprintMaterial();
            if (footprintMaterial == null) return null;
        }

        // Create a simple quad for footprint visualization
        GameObject footprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
        footprint.transform.position = position;
        footprint.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
        footprint.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate to lie flat on the ground

        // Remove the collider since we don't need it for footprints
        Destroy(footprint.GetComponent<Collider>());

        // Assign the footprint material
        MeshRenderer renderer = footprint.GetComponent<MeshRenderer>();
        renderer.material = new Material(footprintMaterial);

        if (!keepFootprintsInWorldSpace && aircraftTransform != null)
        {
            footprint.transform.parent = aircraftTransform; // Optional local-space mode
        }

        return footprint;
    }

    void OnDestroy()
    {
        // Clean up all footprints
        foreach (var footprint in footprints)
        {
            if (footprint.visualObject != null)
            {
                Destroy(footprint.visualObject);
            }
        }
        footprints.Clear();
    }
}
    
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Numerics;

using Debug = UnityEngine.Debug;
using Color = UnityEngine.Color;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
public class EOIRFootprintTracker : MonoBehaviour
{
    [Header("References")]
    public Camera eoirCamera;
    public Transform aircraftTransform;

    [Header("Footprint Settings")]
    public int maxFootprints = 50;
    public float footprintInterval = 1f; // Time in seconds between footprint updates
    public float footprintLifetime = 60f; // Fade after this many seconds
    public float maxDistance = 50000f; // Max distance from aircraft to place footprints (in meters)
    [Tooltip("Only colliders on these layers can receive EOIR footprints. Exclude ship layers here.")]
    public LayerMask footprintSurfaceMask = ~0;

    [Header("Visualization Settings")]
    public GameObject prefabFootprint; // Optional prefab for footprint visualization (should be a simple quad with transparent material)
    public Shader footprintShader;
    public Color recentColor = new Color(0, 1, 1, 0.8f); // Bright cyan
    public Color oldColor = new Color(0, 0.5f, 0.5f, 0.2f); // Faded cyan
    public float footprintSize = 100f; // Meters covered by each footprint
    public float heightOffset = 10f; // Height above sea level to place footprints

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

        // Raycast from camera to find where the footprint should be placed
        RaycastHit hit;
        if (Physics.Raycast(
            eoirCamera.transform.position,
            eoirCamera.transform.forward,
            out hit,
            maxDistance,
            layerMask,
            QueryTriggerInteraction.Ignore))
        {
            // Create new fooprint at hit location
            Footprint footprint = new Footprint
            {
                position = hit.point + Vector3.up * heightOffset,
                timestamp = Time.time,
                visualObject = CreateFootprintVisual(hit.point + Vector3.up * heightOffset)
            };
            footprints.Enqueue(footprint);

            // Remove old footprints
            while (footprints.Count > maxFootprints)
            {
                // Need a function to remove the oldest footprint's visual object and then dequeue it
                RemoveFootprint();
            }
        }
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
            if (age >= footprintLifetime)
            {
                RemoveFootprint();
                continue;
            }

            // Update visual appearance based on age
            if (footprint.visualObject != null)
            {
                float fadePercent = age / footprintLifetime; // 0 = recentColor, 1 = oldColor
                Color currentColor = Color.Lerp(recentColor, oldColor, fadePercent);

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

    GameObject CreateFootprintVisual(Vector3 position)
    {
        if (footprintMaterial == null)
        {
            CreateFootprintMaterial();
            if (footprintMaterial == null) return null;
        }

        // Create a simple quad for footprint visualization
        GameObject footprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
        footprint.transform.position = position;
        footprint.transform.localScale = new Vector3(footprintSize, footprintSize, 1);
        footprint.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate to lie flat on the ground

        // Remove the collider since we don't need it for footprints
        Destroy(footprint.GetComponent<Collider>());

        // Assign the footprint material
        MeshRenderer renderer = footprint.GetComponent<MeshRenderer>();
        renderer.material = new Material(footprintMaterial);

        footprint.transform.parent = aircraftTransform; // Parent to aircraft so it moves with it
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
    
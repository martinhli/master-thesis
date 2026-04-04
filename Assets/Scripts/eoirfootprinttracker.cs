using UnityEngine;
using System.Collections.Generic;
using System.Numerics;
using System.Diagnostics;
using System.Drawing;

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

    [Header("Visualization Settings")]
    public GameObject footprintPrefab;
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
        // Create transparent material for footprints
        footprintMaterial = new Material(Shader.Find("Unlit/Transparent"));
        footprintMaterial.color = recentColor;
        footprintMaterial.renderQueue = 3000; // Ensure it renders on top of most geometry
    }

    void AddFootprint()
    {
        if (eoirCamera == null) return;

        // Raycast from camera to find where the footprint should be placed
        RaycastHit hit;
        if (Physics.Raycast(
            eoirCamera.transform.position,
            eoirCamera.transform.forward,
            out hit,
            maxDistance))
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

            Debug.Log($"[EOIRFootprintTracker] Added footprint at {hit.point}, total footprints: {footprints.Count}");
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

                // Get mesh renderer and update material color
                MeshRenderer renderer = footprint.visualObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material.color = currentColor;
                }
            }
        }
    }

    GameObject CreateFootprintVisual(Vector3 position)
    {
        GameObject footprint;

        if (footprintPrefab != null)
        {
            // Use provided prefab for footprint visualization
            footprint = Instantiate(footprintPrefab, position, Quaternion.Euler(90, 0, 0));
        }
        else
        {
            // Create a simple quad if no prefab is provided
            footprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
            footprint.transform.position = position;
            footprint.transform.localScale = new Vector3(footprintSize, footprintSize, 1);
            footprint.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate to lie flat on the ground
            
            // Remove the collider since we don't need it for footprints
            Destroy(footprint.GetComponent<Collider>());

            // Assign the footprint material
            MeshRenderer renderer = footprint.GetComponent<MeshRenderer>();
            renderer.material = new Material(footprintMaterial);
        }
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
    
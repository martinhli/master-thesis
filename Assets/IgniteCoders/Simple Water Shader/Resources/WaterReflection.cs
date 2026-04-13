using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class WaterReflection : MonoBehaviour
{
    // referenses
    Camera mainCamera;
    Camera reflectionCamera;

    [Tooltip("Optional explicit source camera. If empty, Camera.main is used.")]
    public Camera sourceCamera;

    [Tooltip("The plane where the camera will be reflected, the water plane or any object with the same position and rotation")]
    public Transform reflectionPlane;
    [Tooltip("The texture used by the Water shader to display the reflection")]
    public RenderTexture outputTexture;

    [Tooltip("Force reflection camera to render as mono/offscreen, which is safer for XR projects.")]
    public bool forceReflectionCameraMono = true;

    [Tooltip("Disable the graph's experimental reflection toggle while XR is active to avoid SPI-only artifacts.")]
    public bool disableExperimentalReflectionInXR = false;

    [Tooltip("Optional water renderers to update. If empty, all renderers in this scene are scanned.")]
    public Renderer[] waterRenderers;

    // parameters
    public bool copyCameraParamerers;
    public float verticalOffset;
    private bool isReady;

    // cache
    private Transform mainCamTransform;
    private Transform reflectionCamTransform;

    // Shader Graph default reference names discovered in WaterShader.shadergraph.
    private const string ReflectionTextureProperty = "Texture2D_28de85506601443d82b6148f21ccc69c";
    private const string ReflectionToggleProperty = "Boolean_d3c978b0d14a4f66be175a9b89855be0";

    // Common fallback names if reference names are overridden later.
    private static readonly string[] ReflectionTextureFallbacks =
    {
        "_ReflectionTexture",
        "_ReflectionTex"
    };

    private static readonly string[] ReflectionToggleFallbacks =
    {
        "_UseReflection",
        "_UseReflectionExperimental"
    };

    public void Awake()
    {
        ResolveSourceCamera();

        reflectionCamera = GetComponent<Camera>();

        Validate();
    }

    private void Update()
    {
        ResolveSourceCamera();

        ApplyWaterMaterialOverrides();

        if (isReady)
            RenderReflection();
    }

    private void ResolveSourceCamera()
    {
        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        mainCamera = sourceCamera;

        if (reflectionPlane == null)
        {
            GameObject sea = GameObject.Find("sea");
            if (sea != null)
            {
                reflectionPlane = sea.transform;
            }
        }
    }

    private void RenderReflection()
    {
        // take main camera directions and position world space
        Vector3 cameraDirectionWorldSpace = mainCamTransform.forward;
        Vector3 cameraUpWorldSpace = mainCamTransform.up;
        Vector3 cameraPositionWorldSpace = mainCamTransform.position;

        cameraPositionWorldSpace.y += verticalOffset;

        // transform direction and position by reflection plane
        Vector3 cameraDirectionPlaneSpace = reflectionPlane.InverseTransformDirection(cameraDirectionWorldSpace);
        Vector3 cameraUpPlaneSpace = reflectionPlane.InverseTransformDirection(cameraUpWorldSpace);
        Vector3 cameraPositionPlaneSpace = reflectionPlane.InverseTransformPoint(cameraPositionWorldSpace);

        // invert direction and position by reflection plane
        cameraDirectionPlaneSpace.y *= -1;
        cameraUpPlaneSpace.y *= -1;
        cameraPositionPlaneSpace.y *= -1;

        // transform direction and position from reflection plane local space to world space
        cameraDirectionWorldSpace = reflectionPlane.TransformDirection(cameraDirectionPlaneSpace);
        cameraUpWorldSpace = reflectionPlane.TransformDirection(cameraUpPlaneSpace);
        cameraPositionWorldSpace = reflectionPlane.TransformPoint(cameraPositionPlaneSpace);

        // apply direction and position to reflection camera
        reflectionCamTransform.position = cameraPositionWorldSpace;
        reflectionCamTransform.LookAt(cameraPositionWorldSpace + cameraDirectionWorldSpace, cameraUpWorldSpace);
    }

    private void Validate()
    {
        ResolveSourceCamera();

        if (mainCamera != null)
        {
            mainCamTransform = mainCamera.transform;
            isReady = true;
        }
        else
            isReady = false;

        if (reflectionCamera != null)
        {
            reflectionCamTransform = reflectionCamera.transform;
            isReady = true;
        }
        else
            isReady = false;

        if (isReady && copyCameraParamerers)
        {
            copyCameraParamerers = !copyCameraParamerers;
            reflectionCamera.CopyFrom(mainCamera);

            reflectionCamera.targetTexture = outputTexture;
        }

        if (reflectionCamera != null && forceReflectionCameraMono)
        {
            // Offscreen reflection cameras should not use XR stereo targets.
            reflectionCamera.stereoTargetEye = StereoTargetEyeMask.None;
        }
    }

    private void ApplyWaterMaterialOverrides()
    {
        Renderer[] renderers = waterRenderers;
        if (renderers == null || renderers.Length == 0)
        {
            renderers = FindObjectsOfType<Renderer>();
        }

        bool xrActive = XRSettings.enabled && XRSettings.isDeviceActive;
        bool useReflection = !(disableExperimentalReflectionInXR && xrActive);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererRef = renderers[i];
            if (rendererRef == null)
            {
                continue;
            }

            Material[] materials = rendererRef.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                continue;
            }

            for (int m = 0; m < materials.Length; m++)
            {
                Material mat = materials[m];
                if (mat == null)
                {
                    continue;
                }

                if (outputTexture != null)
                {
                    if (mat.HasProperty(ReflectionTextureProperty))
                    {
                        mat.SetTexture(ReflectionTextureProperty, outputTexture);
                    }

                    for (int t = 0; t < ReflectionTextureFallbacks.Length; t++)
                    {
                        string prop = ReflectionTextureFallbacks[t];
                        if (mat.HasProperty(prop))
                        {
                            mat.SetTexture(prop, outputTexture);
                        }
                    }
                }

                if (mat.HasProperty(ReflectionToggleProperty))
                {
                    mat.SetFloat(ReflectionToggleProperty, useReflection ? 1f : 0f);
                }

                for (int p = 0; p < ReflectionToggleFallbacks.Length; p++)
                {
                    string prop = ReflectionToggleFallbacks[p];
                    if (mat.HasProperty(prop))
                    {
                        mat.SetFloat(prop, useReflection ? 1f : 0f);
                    }
                }
            }
        }
    }
}
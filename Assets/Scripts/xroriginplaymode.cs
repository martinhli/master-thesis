using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

[RequireComponent(typeof(XROrigin))]
public class XROriginPlayMode : MonoBehaviour
{
    [Header("Non-XR Play Mode Fallback")]
    [Tooltip("Used when no XR display subsystem is running in Play mode.")]
    public float fallbackEyeHeight = 1.1176f;

    [Tooltip("Disables TrackedPoseDriver when no XR display is running so the camera pose is not forced to (0,0,0).")]
    public bool disableTrackedPoseDriverWhenNoXR = true;

    private XROrigin _xrOrigin;
    private TrackedPoseDriver _trackedPoseDriver;

    private void Awake()
    {
        _xrOrigin = GetComponent<XROrigin>();
        if (_xrOrigin != null && _xrOrigin.Camera != null)
        {
            _trackedPoseDriver = _xrOrigin.Camera.GetComponent<TrackedPoseDriver>();
        }
    }

    private void Start()
    {
        ApplyMode();
    }

    private void OnEnable()
    {
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool xrRunning = IsXRRunning();

        if (_trackedPoseDriver != null && disableTrackedPoseDriverWhenNoXR)
        {
            _trackedPoseDriver.enabled = xrRunning;
        }

        if (!xrRunning && _xrOrigin != null && _xrOrigin.CameraFloorOffsetObject != null)
        {
            Vector3 p = _xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            p.y = fallbackEyeHeight;
            _xrOrigin.CameraFloorOffsetObject.transform.localPosition = p;
        }
    }

    private static bool IsXRRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);

        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running)
            {
                return true;
            }
        }

        return false;
    }
}

using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class movecursor : MonoBehaviour
{
    public Camera mainCamera;

    public float sensitivity = 2.0f;
    public float smoothing = 1.5f;
    public bool lockCursorOnStart = false;
    public KeyCode toggleCursorKey = KeyCode.Tab;
    public bool allowLookWhileCursorUnlocked = true;
    public bool requireRightMouseForUnlockedLook = true;

    private Vector2 velocity;
    private Vector2 desiredRotation;

    void Start()
    {
        // If no camera is assigned, use the main camera in the scene
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        SetCursorLock(lockCursorOnStart);
    }

    void Update()
    {
        if (WasToggleKeyPressed())
        {
            bool lockNow = Cursor.lockState != CursorLockMode.Locked;
            SetCursorLock(lockNow);
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }

        bool canRotateCamera = Cursor.lockState == CursorLockMode.Locked || CanLookWhileUnlocked();
        if (!canRotateCamera)
        {
            return;
        }

        // Get mouse input (delta movement)
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        // Apply sensitivity and smoothing
        mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity * smoothing, sensitivity * smoothing));
        velocity.x = Mathf.Lerp(velocity.x, mouseDelta.x, 1f / smoothing);
        velocity.y = Mathf.Lerp(velocity.y, mouseDelta.y, 1f / smoothing);
        desiredRotation += velocity;
        
        // Clamp vertical rotation so you can't flip the camera upside down
        desiredRotation.y = Mathf.Clamp(desiredRotation.y, -90f, 90f);

        // Apply rotations
        // Vertical rotation (around X-axis) to the camera itself
        transform.localRotation = Quaternion.AngleAxis(-desiredRotation.y, Vector3.right);
        // Horizontal rotation (around Y-axis) is often applied to a parent object (like a Player body)
        // If this script is only on the camera and you want full 3D rotation, you can use:
        transform.localRotation = Quaternion.Euler(-desiredRotation.y, desiredRotation.x, 0);
    }

    private bool CanLookWhileUnlocked()
    {
        if (!allowLookWhileCursorUnlocked)
        {
            return false;
        }

        if (!requireRightMouseForUnlockedLook)
        {
            return true;
        }

        if (Mouse.current != null)
        {
            return Mouse.current.rightButton.isPressed;
        }

        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    private void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;

        if (locked && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private bool WasToggleKeyPressed()
    {
        if (toggleCursorKey == KeyCode.Tab && Keyboard.current != null)
        {
            return Keyboard.current.tabKey.wasPressedThisFrame;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        return toggleCursorKey switch
        {
            KeyCode.Escape => Keyboard.current.escapeKey.wasPressedThisFrame,
            KeyCode.LeftShift => Keyboard.current.leftShiftKey.wasPressedThisFrame,
            KeyCode.RightShift => Keyboard.current.rightShiftKey.wasPressedThisFrame,
            KeyCode.LeftControl => Keyboard.current.leftCtrlKey.wasPressedThisFrame,
            KeyCode.RightControl => Keyboard.current.rightCtrlKey.wasPressedThisFrame,
            KeyCode.LeftAlt => Keyboard.current.leftAltKey.wasPressedThisFrame,
            KeyCode.RightAlt => Keyboard.current.rightAltKey.wasPressedThisFrame,
            _ => false,
        };
    }
}

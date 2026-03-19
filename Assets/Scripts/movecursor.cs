using System.Runtime.CompilerServices;
using UnityEngine;

public class movecursor : MonoBehaviour
{
    public Camera mainCamera;

    public float sensitivity = 2.0f;
    public float smoothing = 1.5f;

    private Vector2 velocity;
    private Vector2 desiredRotation;

    void Start()
    {
        // If no camera is assigned, use the main camera in the scene
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        // Lock the cursor to the center of the screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Get mouse input (delta movement)
        Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

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
}

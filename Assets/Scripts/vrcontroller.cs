using UnityEngine;
using UnityEngine.XR;

public class VRControllerTest : MonoBehaviour
{
    private InputDevice rightController;
    private InputDevice leftController;
    
    void Start()
    {
        // Get controllers
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        Debug.Log("VR Controller Test Started!");
        Debug.Log("Right Controller: " + rightController.name);
        Debug.Log("Left Controller: " + leftController.name);
    }
    
    void Update()
    {
        TestRightController();
        TestLeftController();
    }
    
    void TestRightController()
    {
        // A Button
        bool aButton;
        if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out aButton))
        {
            if (aButton)
            {
                Debug.Log("RIGHT: A Button Pressed!");
            }
        }
        
        // B Button
        bool bButton;
        if (rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bButton))
        {
            if (bButton)
            {
                Debug.Log("RIGHT: B Button Pressed!");
            }
        }
        
        // Thumbstick
        Vector2 thumbstick;
        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick))
        {
            if (thumbstick.magnitude > 0.1f)
            {
                Debug.Log("RIGHT Thumbstick: " + thumbstick);
            }
        }
        
        // Trigger
        float trigger;
        if (rightController.TryGetFeatureValue(CommonUsages.trigger, out trigger))
        {
            if (trigger > 0.1f)
            {
                Debug.Log("RIGHT Trigger: " + trigger);
            }
        }
        
        // Grip
        float grip;
        if (rightController.TryGetFeatureValue(CommonUsages.grip, out grip))
        {
            if (grip > 0.1f)
            {
                Debug.Log("RIGHT Grip: " + grip);
            }
        }
    }
    
    void TestLeftController()
    {
        // X Button
        bool xButton;
        if (leftController.TryGetFeatureValue(CommonUsages.primaryButton, out xButton))
        {
            if (xButton)
            {
                Debug.Log("LEFT: X Button Pressed!");
            }
        }
        
        // Y Button
        bool yButton;
        if (leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out yButton))
        {
            if (yButton)
            {
                Debug.Log("LEFT: Y Button Pressed!");
            }
        }
        
        // Thumbstick
        Vector2 thumbstick;
        if (leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick))
        {
            if (thumbstick.magnitude > 0.1f)
            {
                Debug.Log("LEFT Thumbstick: " + thumbstick);
            }
        }
    }
}

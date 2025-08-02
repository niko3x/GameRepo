using UnityEngine;

public class VisualSphereRoller : MonoBehaviour
{
    [Header("Rolling Settings")]
    public float sphereRadius = 0.5f; // Radius of your sphere (adjust to match your sphere's actual radius)
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    private Vector3 lastPosition;
    private Vector2 moveInput;
    
    void Start()
    {
        // Make sure this GameObject has a MeshRenderer and MeshFilter
        if (GetComponent<MeshRenderer>() == null)
        {
            Debug.LogWarning("VisualSphereRoller: No MeshRenderer found on " + gameObject.name);
        }
        
        // Make sure this GameObject does NOT have a collider
        if (GetComponent<Collider>() != null)
        {
            Debug.LogWarning("VisualSphereRoller: This GameObject should not have a Collider. The PlayerController handles physics.");
        }
        
        // Initialize last position
        lastPosition = transform.parent.position;
    }
    
    void Update()
    {
        // Get the current position of the parent (the actual player position)
        Vector3 currentPosition = transform.parent.position;
        
        // Calculate the movement delta
        Vector3 deltaMovement = currentPosition - lastPosition;
        
        // Only roll if there's actual movement
        if (deltaMovement.magnitude > 0.001f)
        {
            // Calculate rotation based on actual movement
            // For a sphere rolling on the ground, we need to rotate around axes perpendicular to movement
            
            // Forward/backward movement rotates around the X axis (local right)
            float xRotation = deltaMovement.z / sphereRadius * Mathf.Rad2Deg;
            
            // Left/right movement rotates around the Z axis (local forward)  
            float zRotation = -deltaMovement.x / sphereRadius * Mathf.Rad2Deg;
            
            // Apply the rotation in world space to avoid axis drift
            transform.Rotate(Vector3.right, xRotation, Space.World);
            transform.Rotate(Vector3.forward, zRotation, Space.World);
            
            if (showDebugInfo)
            {
                Debug.Log($"Movement: {deltaMovement}, X Rot: {xRotation:F2}, Z Rot: {zRotation:F2}");
            }
        }
        
        // Update last position
        lastPosition = currentPosition;
    }
    
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }
    
    // Helper method to reset the sphere's rotation if needed
    [ContextMenu("Reset Rotation")]
    public void ResetRotation()
    {
        transform.rotation = Quaternion.identity;
    }
}
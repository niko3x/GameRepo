using UnityEngine;

public class VisualSphereRoller : MonoBehaviour
{
    [Header("Rolling Settings")]
    public float rollSpeed = 360f;
    
    [Header("Rolling Axes (check to enable rotation)")]
    public bool rollOnX = true;  // Forward/Backward (W/S)
    public bool rollOnY = false; // Up/Down rotation
    public bool rollOnZ = true;  // Left/Right (A/D)
    
    [Header("Axis Multipliers (negative to reverse)")]
    public float xAxisMultiplier = -1f;  // Forward/Backward multiplier
    public float yAxisMultiplier = 0f;   // Up/Down multiplier  
    public float zAxisMultiplier = 1f;   // Left/Right multiplier
    
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
    }
    
    void Update()
    {
        // Calculate rotation for each axis using WORLD SPACE axes
        float xRotation = rollOnX ? moveInput.y * xAxisMultiplier * rollSpeed * Time.deltaTime : 0f;
        float yRotation = rollOnY ? moveInput.x * yAxisMultiplier * rollSpeed * Time.deltaTime : 0f;
        float zRotation = rollOnZ ? moveInput.x * zAxisMultiplier * rollSpeed * Time.deltaTime : 0f;
        
        // Apply rotation in WORLD SPACE so axes don't get fucked up
        if (xRotation != 0) transform.Rotate(Vector3.right, xRotation, Space.World);
        if (yRotation != 0) transform.Rotate(Vector3.up, yRotation, Space.World);
        if (zRotation != 0) transform.Rotate(Vector3.forward, zRotation, Space.World);
    }
    
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }
}
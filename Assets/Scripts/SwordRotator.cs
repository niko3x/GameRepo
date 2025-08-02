using UnityEngine;

public class SwordRotator : MonoBehaviour
{
    [Header("References")]
    public Transform playerObject; // Drag the player GameObject here
    
    [Header("Rotation Settings")]
    public float orbitDistance = 1.5f; // Distance from player center
    public bool smoothRotation = true;
    public float rotationSmoothness = 10f;
    
    [Header("Debug")]
    public bool showDebugLines = false;
    
    private Vector2 rotationInput;
    private float currentAngle = 0f; // Current angle around player
    private float targetAngle = 0f; // Target angle based on joystick
    private float swordHeight; // Store initial height offset
    
    public static SwordRotator Instance;
    
    void Awake()
    {
        Instance = this;
    }
    
    void Start()
    {
        // If no player object assigned, try to get parent
        if (playerObject == null)
        {
            playerObject = transform.parent;
            if (playerObject != null)
            {
                Debug.Log("SwordRotator: Using parent as player object");
            }
        }
        
        if (playerObject == null)
        {
            Debug.LogError("SwordRotator: No player object assigned! Please drag the player GameObject to the Player Object field.");
            return;
        }
        
        // Calculate initial angle and distance
        Vector3 offset = transform.position - playerObject.position;
        currentAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        targetAngle = currentAngle;
        orbitDistance = new Vector2(offset.x, offset.z).magnitude;
        swordHeight = offset.y; // Store height difference
        
        Debug.Log($"SwordRotator initialized - Initial angle: {currentAngle:F1}°, Distance: {orbitDistance:F2}");
    }
    
    void Update()
    {
        if (playerObject == null) return;
        
        // Handle rotation input
        HandleRotation();
        
        // Update sword position and rotation
        UpdateSwordTransform();
        
        // Debug visualization
        if (showDebugLines)
        {
            DrawDebugLines();
        }
    }
    
    void HandleRotation()
    {
        if (rotationInput.magnitude > 0.1f) // Dead zone to prevent tiny movements
        {
            // Convert joystick input to angle
            // Joystick X = left/right, Joystick Y = forward/backward
            targetAngle = Mathf.Atan2(rotationInput.y, rotationInput.x) * Mathf.Rad2Deg;
            
            // Smooth angle transition
            if (smoothRotation)
            {
                // Handle angle wrapping (shortest path between angles)
                float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
                currentAngle += angleDiff * rotationSmoothness * Time.deltaTime;
            }
            else
            {
                currentAngle = targetAngle;
            }
        }
    }
    
    void UpdateSwordTransform()
    {
        // Calculate position around the player
        float angleInRadians = currentAngle * Mathf.Deg2Rad;
        
        Vector3 orbitPosition = new Vector3(
            Mathf.Cos(angleInRadians) * orbitDistance,
            swordHeight, // Keep original height offset
            Mathf.Sin(angleInRadians) * orbitDistance
        );
        
        // Set position relative to player
        transform.position = playerObject.position + orbitPosition;
        
        // Make sword always point away from player (tip pointing outward)
        Vector3 directionFromPlayer = (transform.position - playerObject.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(-directionFromPlayer, Vector3.up);
        
        if (smoothRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSmoothness * Time.deltaTime);
        }
        else
        {
            transform.rotation = lookRotation;
        }
    }
    
    public void SetRotationInput(Vector2 input)
    {
        rotationInput = input;
        
        // Debug
        if (input.magnitude > 0.1f)
        {
            float inputAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            Debug.Log($"Sword input: {input}, Input angle: {inputAngle:F1}°, Current: {currentAngle:F1}°");
        }
    }
    
    void DrawDebugLines()
    {
        if (playerObject == null) return;
        
        // Draw line from player to sword
        Debug.DrawLine(playerObject.position, transform.position, Color.yellow);
        
        // Draw sword forward direction (tip direction)
        Debug.DrawRay(transform.position, transform.forward * 1.5f, Color.red);
        
        // Draw orbit circle
        for (int i = 0; i < 36; i++)
        {
            float angle1 = i * 10f * Mathf.Deg2Rad;
            float angle2 = (i + 1) * 10f * Mathf.Deg2Rad;
            
            Vector3 point1 = playerObject.position + new Vector3(
                Mathf.Cos(angle1) * orbitDistance,
                swordHeight,
                Mathf.Sin(angle1) * orbitDistance
            );
            
            Vector3 point2 = playerObject.position + new Vector3(
                Mathf.Cos(angle2) * orbitDistance,
                swordHeight,
                Mathf.Sin(angle2) * orbitDistance
            );
            
            Debug.DrawLine(point1, point2, Color.cyan);
        }
        
        // Draw target direction
        if (rotationInput.magnitude > 0.1f)
        {
            float targetAngleRad = targetAngle * Mathf.Deg2Rad;
            Vector3 targetPos = playerObject.position + new Vector3(
                Mathf.Cos(targetAngleRad) * orbitDistance,
                swordHeight,
                Mathf.Sin(targetAngleRad) * orbitDistance
            );
            Debug.DrawLine(playerObject.position, targetPos, Color.green);
        }
    }
    
    [ContextMenu("Reset Sword Position")]
    public void ResetSwordPosition()
    {
        currentAngle = 0f;
        targetAngle = 0f;
        UpdateSwordTransform();
    }
}
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    
    [Header("Jump Settings")]
    public float jumpForce = 10f;
    public float gravity = 20f;
    public LayerMask groundLayer = 1; // What counts as "ground"
    
    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f; // Distance to check for ground (adjust based on sphere size)
    public bool showGroundCheckDebug = false;
    
    [Header("Setup Instructions")]
    [TextArea(3, 5)]
    public string setupInstructions = "SETUP:\n1. Remove Rigidbody from this GameObject\n2. Set ground objects to 'Ground' layer\n3. Set Ground Layer below to match\n4. Adjust Ground Check Distance to sphere radius + 0.1";
    
    private Vector2 moveInput;
    private VisualSphereRoller visualSphereRoller;
    private Vector3 velocity;
    private bool isGrounded;
    private CharacterController characterController;
    
    void Start()
    {
        visualSphereRoller = GetComponentInChildren<VisualSphereRoller>();
        
        // Try to get CharacterController, if not found, add one
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            // Set up CharacterController for a sphere
            characterController.radius = 0.5f;
            characterController.height = 1f;
            characterController.center = Vector3.zero;
            Debug.Log("Added CharacterController to PlayerController");
        }
        
        
    }
    
    void Update()
    {
        // Ground check
        CheckGrounded();
        
        // Get input from our custom joystick
        if (SimpleJoystick.Instance != null)
        {
            moveInput = SimpleJoystick.Instance.GetInput();
        }
        else
        {
            // Fallback to keyboard for testing
            moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }
        
        // Handle jumping
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }
        
        // Handle movement and physics
        HandleMovement();
        
        // Send input to visual sphere
        if (visualSphereRoller != null)
        {
            visualSphereRoller.SetMoveInput(moveInput);
        }
        
        // Debug info
        if (showGroundCheckDebug)
        {
            Debug.Log($"Grounded: {isGrounded}, Velocity Y: {velocity.y:F2}");
        }
    }
    
    void CheckGrounded()
    {
        // Cast a ray downward to check if we're on the ground
        Vector3 rayStart = transform.position;
        float rayDistance = groundCheckDistance;
        
        isGrounded = Physics.Raycast(rayStart, Vector3.down, rayDistance, groundLayer);
        
        // Debug visualization
        if (showGroundCheckDebug)
        {
            Color rayColor = isGrounded ? Color.green : Color.red;
            Debug.DrawRay(rayStart, Vector3.down * rayDistance, rayColor);
        }
    }
    
    void Jump()
    {
        velocity.y = jumpForce;
        Debug.Log("Jump!");
    }
    
    void HandleMovement()
    {
        // Horizontal movement
        Vector3 horizontalMovement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed;
        
        // Apply gravity
        if (!isGrounded)
        {
            velocity.y -= gravity * Time.deltaTime;
        }
        else if (velocity.y < 0)
        {
            velocity.y = 0; // Stop falling when on ground
        }
        
        // Combine horizontal and vertical movement
        Vector3 finalMovement = horizontalMovement + new Vector3(0, velocity.y, 0);
        
        // Apply movement using CharacterController
        characterController.Move(finalMovement * Time.deltaTime);
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize ground check in scene view
        if (showGroundCheckDebug)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);
        }
    }
}
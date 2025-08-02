using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    
    private Vector2 moveInput;
    private VisualSphereRoller visualSphereRoller;
    
    void Start()
    {
        visualSphereRoller = GetComponentInChildren<VisualSphereRoller>();
        
        if (GetComponent<SphereCollider>() == null)
        {
            Debug.LogWarning("PlayerController: No SphereCollider found. Add one to this GameObject for proper physics.");
        }
    }
    
    void Update()
    {
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
        
        // Apply movement
        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(movement);
        
        // Send input to visual sphere
        if (visualSphereRoller != null)
        {
            visualSphereRoller.SetMoveInput(moveInput);
        }
        
        // Debug movement
        if (moveInput.magnitude > 0)
        {
            Debug.Log("Moving: " + moveInput);
        }
    }
}
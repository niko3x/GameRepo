using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BrawlStarsJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Settings")]
    public float radius = 100f;
    public Vector2 positionOffset = Vector2.zero; // Adjust this to fix positioning
    
    [Header("UI References")]
    public RectTransform background;
    public RectTransform handle;
    public RectTransform touchArea;
    
    private Vector2 inputVector = Vector2.zero;
    private Canvas canvas;
    private Camera cam;
    private bool isDragging = false;
    private Vector2 joystickCenter;
    
    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        
        background.gameObject.SetActive(false);
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // Get touch position in world coordinates
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            touchArea, 
            eventData.position, 
            cam, 
            out Vector2 touchPosition);
        
        // Apply position offset
        touchPosition += positionOffset;
        
        // Position joystick at touch point
        background.anchoredPosition = touchPosition;
        background.gameObject.SetActive(true);
        
        // Store joystick center position
        joystickCenter = background.anchoredPosition;
        
        isDragging = true;
        OnDrag(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            cam,
            out Vector2 touchPosition);
            
        // Calculate movement vector from center
        Vector2 delta = touchPosition;
        float distance = Mathf.Clamp(delta.magnitude, 0f, radius);
        Vector2 direction = delta.normalized;
        
        // Calculate input vector (normalized)
        inputVector = direction * (distance / radius);
        
        // Move handle within radius
        handle.anchoredPosition = direction * distance;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        background.gameObject.SetActive(false);
    }
    
    public Vector2 GetInput() => inputVector;
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    
    // Editor helper to visualize offset
    #if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && touchArea != null)
        {
            Vector3[] corners = new Vector3[4];
            touchArea.GetWorldCorners(corners);
            
            Vector3 center = (corners[0] + corners[2]) / 2;
            Vector3 offsetPosition = center + new Vector3(positionOffset.x, positionOffset.y, 0);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(offsetPosition, 20f);
            Gizmos.DrawLine(center, offsetPosition);
        }
    }
    #endif
}
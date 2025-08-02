using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SimpleJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Settings")]
    public float radius = 100f;
    
    [Header("UI References")]
    public RectTransform background; // Joystick base image
    public RectTransform handle; // Joystick handle image
    public RectTransform touchArea; // Touch detection area (your first image)
    
    private Vector2 inputVector = Vector2.zero;
    private Canvas canvas;
    private Camera cam;
    private bool isDragging = false;
    private Vector2 backgroundStartPosition;
    
    public static SimpleJoystick Instance;
    
    void Awake()
    {
        Instance = this;
    }
    
    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        
        // Hide joystick at start
        background.gameObject.SetActive(false);
        backgroundStartPosition = background.anchoredPosition;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // Convert screen position to world position
        Vector3 worldPosition;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            touchArea, 
            eventData.position, 
            cam, 
            out worldPosition))
        {
            // Convert world position to local position relative to background's parent
            RectTransform parentRect = background.parent as RectTransform;
            Vector2 localPosition = parentRect.InverseTransformPoint(worldPosition);
            
            background.anchoredPosition = localPosition;
        }
        
        background.gameObject.SetActive(true);
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
            out Vector2 localPoint);
        
        // Calculate movement vector
        Vector2 delta = localPoint;
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
}
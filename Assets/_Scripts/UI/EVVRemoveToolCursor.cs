using UnityEngine;

// World-space icon that sits at a fixed "home" position (its scene placement) and, while the
// remove tool is active, detaches and follows the mouse instead - replacing the system cursor
// with its own sprite. Driven by PlantPlacementManager via FollowCursor(bool); ContainsPoint lets
// PlantPlacementManager detect a click on the icon itself while it's still at its home position.
[RequireComponent(typeof(SpriteRenderer))]
public class EVVRemoveToolCursor : MonoBehaviour
{
    bool removeToolIsActive;
    Vector3 originalPosition;
    bool hasOriginalPosition;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        CaptureOriginalPosition();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Awake() order between this and PlantPlacementManager (which calls FollowCursor from its own
    // Awake) isn't guaranteed, so capturing originalPosition only in Awake risked reading this
    // transform's position AFTER FollowCursor(false) had already moved it to (0,0,0) - snapping
    // the icon to the world origin and permanently baking that in as its "home" position. Capture
    // lazily instead so whichever caller runs first gets the real starting position.
    void CaptureOriginalPosition()
    {
        if (hasOriginalPosition)
        {
            return;
        }

        originalPosition = transform.position;
        hasOriginalPosition = true;
    }

    // Used to detect a click on the icon while it's sitting at its home position (i.e. not
    // already following the mouse as the active remove-tool cursor).
    public bool ContainsPoint(Vector3 worldPosition)
    {
        return spriteRenderer != null && spriteRenderer.bounds.Contains(worldPosition);
    }

    void Update()
    {
        if (!removeToolIsActive)
        {
            return;
        }

        MoveToMouse();
    }

    void MoveToMouse()
    {
        var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mousePos.x, mousePos.y, transform.position.z);
    }

    public void FollowCursor(bool isOn)
    {
        CaptureOriginalPosition();

        if (isOn)
            MoveToMouse();
        else
            transform.position = originalPosition;
        removeToolIsActive = isOn;
        Cursor.visible = !isOn;
    }

    void OnDisable()
    {
        if (removeToolIsActive)
            Cursor.visible = true;
    }

    void OnEnable()
    {
        if (removeToolIsActive)
            Cursor.visible = false;
    }

}

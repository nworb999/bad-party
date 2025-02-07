using UnityEngine;

public class PlayerMovementListener : MonoBehaviour
{
    // Reference to the player's components
    private Transform playerTransform;
    private Rigidbody playerRigidbody; // Use Rigidbody2D for 2D games
    private Vector3 lastPosition;

    void Start()
    {
        // Get references
        playerTransform = transform;
        playerRigidbody = GetComponent<Rigidbody>();
        lastPosition = playerTransform.position;
    }

    void Update()
    {
        // Method 1: Listen to Input directly
        if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0) { }

        // Method 2: Check position change
        if (Vector3.Distance(lastPosition, playerTransform.position) > 0.01f)
        {
            Debug.Log($"Movement delta: {playerTransform.position - lastPosition}");
        }
        lastPosition = playerTransform.position;

        // Method 3: Check velocity (if using physics)
        if (playerRigidbody != null && playerRigidbody.velocity.magnitude > 0.01f)
        {
            Debug.Log($"Player velocity: {playerRigidbody.velocity}");
        }
    }

    // Optional: Event system for movement
    public delegate void MovementHandler(Vector3 movementDelta);
    public event MovementHandler OnPlayerMoved;

    void FixedUpdate()
    {
        // Trigger movement event if position changed
        Vector3 movementDelta = playerTransform.position - lastPosition;
        if (movementDelta.magnitude > 0.01f)
        {
            OnPlayerMoved?.Invoke(movementDelta);
        }
    }
}

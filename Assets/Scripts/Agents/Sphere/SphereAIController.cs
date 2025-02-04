using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SphereMovement))]
public class SphereAIController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveRadius = 10f;         // Maximum distance to move
    public float waitTime = 2f;            // Time to wait between movements
    public GameObject centerObject;         // Center object for movement radius
    public GameObject[] locationObjects;    // Array of GameObjects to move between

    [Header("Collision Settings")]
    public LayerMask obstacleLayer;           // Layer mask for obstacles
    public float bufferDistance = 0.5f;   

    private float minWalkTime = 3f;
    private float maxWalkTime = 5f;
    private float minStandTime = 0.5f;
    private float maxStandTime = 1.5f;
    private float lookAroundWaitTime = 0.5f;
    public int currentPointIndex = 0;     // Index of current location point
    private bool isWaiting = false;

    private SphereMovement movement;
    private float stateStartTime;
    private MovementState currentState;

    private enum MovementState
    {
        Walking,
        Standing
    }

    private void Start()
    {
        movement = GetComponent<SphereMovement>();
        if (movement == null)
        {
            Debug.LogError("SphereMovement component not found!");
            enabled = false;
            return;
        }

        if (locationObjects == null || locationObjects.Length == 0)
        {
            Debug.LogWarning("No location objects set, falling back to random movement");
            locationObjects = new GameObject[] { gameObject }; // Use self as fallback point
        }

        if (centerObject == null)
        {
            Debug.LogWarning("No center object set, using this object's position");
            centerObject = gameObject;
        }

        stateStartTime = Time.time;
        currentState = MovementState.Walking; // Start with walking state
        SetNewDestination(); // Set initial destination immediately
        StartCoroutine(MovementLoop());
    }

    private void Update()
    {
        if (!isWaiting)
        {
            switch (currentState)
            {
                case MovementState.Walking: HandleWalking(); break;
                case MovementState.Standing: HandleStanding(); break;
            }
        }
    }

    private void HandleWalking()
    {
        if (movement.HasReachedTarget())
        {
            StartCoroutine(WaitInPlace());
        }
        else if (movement.GetTargetPosition() == Vector3.zero) // Initial or invalid target
        {
            SetNewDestination();
        }
    }

    private void HandleStanding() 
    {
        // Just wait until the movement loop changes state
    }

    private void SetNewDestination()
    {
        if (locationObjects.Length > 1)
        {
            // Move to next point in sequence
            currentPointIndex = (currentPointIndex + 1) % locationObjects.Length;
            movement.MoveToObject(locationObjects[currentPointIndex]);
        }
        else
        {
            // Try random movement
            if (!movement.TryMoveToRandomPoint(centerObject.transform.position, moveRadius, locationObjects))
            {
                // If no valid position found, stay in place
                movement.SetTargetPosition(transform.position);
            }
        }
    }

    private IEnumerator WaitInPlace()
    {
        isWaiting = true;
        ChangeState(MovementState.Standing);
        
        // Look around occasionally while waiting
        float waitStart = Time.time;
        while (Time.time - waitStart < waitTime)
        {
            if (Random.value < 0.3f)
            {
                Vector3 randomLook = transform.position + Random.insideUnitSphere * 5f;
                randomLook.y = transform.position.y;
                transform.LookAt(randomLook);
            }
            yield return new WaitForSeconds(lookAroundWaitTime);
        }

        isWaiting = false;
        ChangeState(MovementState.Walking);
        SetNewDestination();
    }

    private IEnumerator MovementLoop()
    {
    while (true)
    {
        // Shorter waits between state changes
        if (currentState == MovementState.Walking)
        {
            yield return new WaitForSeconds(Random.Range(minWalkTime, maxWalkTime));
            ChangeState(MovementState.Standing);
        }
        else
        {
            yield return new WaitForSeconds(Random.Range(minStandTime, maxStandTime)); // Shorter standing time
            ChangeState(MovementState.Walking);
        }
    }
    }

    private void ChangeState(MovementState newState)
    {
        currentState = newState;
        stateStartTime = Time.time;
        
        if (newState == MovementState.Standing)
        {
            movement.StopMovement();
        }
    }
}
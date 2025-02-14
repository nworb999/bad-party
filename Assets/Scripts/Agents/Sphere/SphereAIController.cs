using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereMovement))]
public class SphereAIController : MonoBehaviour
{
    [Serializable]
    private class StateChangeData
    {
        public string state;
        public Vector3 position;

        public StateChangeData(string state, Vector3 position)
        {
            this.state = state;
            this.position = position;
        }
    }

    [Serializable]
    private class DestinationEventData
    {
        public string state;
        public Vector3 position;
        public string targetName;

        public DestinationEventData(string state, Vector3 position, string targetName)
        {
            this.state = state;
            this.position = position;
            this.targetName = targetName;
        }
    }

    [Header("Agent Settings")]
    public string agentId = "sphere_1";

    [Header("Movement Settings")]
    public float moveRadius = 10f; // Maximum distance to move
    public float waitTime = 2f; // Time to wait between movements
    public GameObject centerObject; // Center object for movement radius
    public GameObject[] locationObjects; // Array of GameObjects to move between

    [Header("Collision Settings")]
    public LayerMask obstacleLayer; // Layer mask for obstacles
    public float bufferDistance = 0.5f;

    private float minWalkTime = 3f;
    private float maxWalkTime = 5f;
    private float minStandTime = 0.5f;
    private float maxStandTime = 1.5f;
    private float lookAroundWaitTime = 0.5f;
    public int currentPointIndex = 0; // Index of current location point
    private bool isWaiting = false;

    private SphereMovement movement;
    private float stateStartTime;
    private MovementState currentState;

    private NetworkServer networkServer;

    private enum MovementState
    {
        Walking,
        Standing,
    }

    private void Start()
    {
        movement = GetComponent<SphereMovement>();
        networkServer = GameObject.FindObjectOfType<NetworkServer>();

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

        if (networkServer == null)
        {
            Debug.LogWarning("NetworkServer not found in scene!");
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
                case MovementState.Walking:
                    HandleWalking();
                    break;
                case MovementState.Standing:
                    HandleStanding();
                    break;
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
        string targetName;
        if (locationObjects.Length > 1)
        {
            // Move to next point in sequence
            currentPointIndex = (currentPointIndex + 1) % locationObjects.Length;
            movement.MoveToObject(locationObjects[currentPointIndex]);
            targetName = locationObjects[currentPointIndex].name;
        }
        else
        {
            // Try random movement
            if (!movement.TryMoveToRandomPoint(centerObject.transform.position, moveRadius, locationObjects))
            {
                // If no valid position found, stay in place
                movement.SetTargetPosition(transform.position);
                targetName = "None";
            }
            else
            {
                targetName = "RandomPoint";
            }
        }

        if (networkServer != null)
        {
            string eventType = "destination_change";
            string agentId = this.agentId;
            string data = JsonUtility.ToJson(new DestinationEventData(
                "destination_change",
                movement.GetTargetPosition(),
                targetName
            ));
            string message = $"{eventType}|{agentId}|{data}";
            networkServer.SendUpdate(message);
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
            if (UnityEngine.Random.value < 0.3f)
            {
                Vector3 randomLook = transform.position + UnityEngine.Random.insideUnitSphere * 5f;
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
                yield return new WaitForSeconds(UnityEngine.Random.Range(minWalkTime, maxWalkTime));
                ChangeState(MovementState.Standing);
            }
            else
            {
                yield return new WaitForSeconds(
                    UnityEngine.Random.Range(minStandTime, maxStandTime)
                ); // Shorter standing time
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

        // Send state change event to Python server
        if (networkServer != null)
        {
            string eventType = "state_change";
            string agentId = this.agentId;
            string data = JsonUtility.ToJson(new StateChangeData(newState.ToString(), transform.position));
            string message = $"{eventType}|{agentId}|{data}";
            networkServer.SendUpdate(message);
        }
    }
}

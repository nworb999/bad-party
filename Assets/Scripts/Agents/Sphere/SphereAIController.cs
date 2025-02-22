using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;

[RequireComponent(typeof(SphereMovement))]
public class SphereAIController : MonoBehaviour
{
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

    [Header("Proximity Settings")]
    public float itemProximityDistance = 2f;
    public float locationProximityDistance = 3f;
    public float characterProximityDistance = 4f;
    public float checkInterval = 0.5f;

    private float lookAroundWaitTime = 0.5f;
    public int currentPointIndex = 0; // Index of current location point
    private bool isWaiting = false;

    private SphereMovement movement;
    private float stateStartTime;
    private MovementState currentState;

    private WebSocketManager webSocketManager;

    private HashSet<string> nearbyItems = new HashSet<string>();
    private HashSet<string> nearbyLocations = new HashSet<string>();
    private HashSet<string> nearbyAgents = new HashSet<string>();

    private enum MovementState
    {
        Walking,
        Standing,
    }

    private void Start()
    {
        movement = GetComponent<SphereMovement>();
        webSocketManager = GameObject.FindObjectOfType<WebSocketManager>();

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

        if (webSocketManager == null)
        {
            Debug.LogWarning("WebSocketManager not found in scene!");
        }

        stateStartTime = Time.time;
        currentState = MovementState.Walking; // Start with walking state
        SetNewDestination(); // Set initial destination immediately
        StartCoroutine(ProximityCheckLoop());
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
        string targetName = "RandomPoint";
        if (locationObjects.Length > 1)
        {
            currentPointIndex = (currentPointIndex + 1) % locationObjects.Length;
            var targetLocation = locationObjects[currentPointIndex];
            movement.MoveToObject(targetLocation);
            targetName = targetLocation.name;
            
            // Send location reached event when destination is set to a named location
            webSocketManager?.SendLocationReached(agentId, targetName, targetLocation.transform.position);
        }
        else
        {
            if (!movement.TryMoveToRandomPoint(centerObject.transform.position, moveRadius, locationObjects))
            {
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

    private IEnumerator ProximityCheckLoop()
    {
        while (true)
        {
            CheckItemProximity();
            CheckLocationProximity();
            CheckAgentProximity();
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void CheckItemProximity()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, itemProximityDistance);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Item") && !nearbyItems.Contains(hitCollider.name))
            {
                nearbyItems.Add(hitCollider.name);
                webSocketManager?.SendProximityEvent(agentId, "item_near", hitCollider.name, transform.position);
            }
        }
        
        // Check for exited items
        nearbyItems.RemoveWhere(itemId => 
            !Array.Exists(hitColliders, c => c.name == itemId));
    }

    private void CheckLocationProximity()
    {
        foreach (var location in locationObjects)
        {
            if (location == null) continue;
            
            float distance = Vector3.Distance(transform.position, location.transform.position);
            bool isNear = distance <= locationProximityDistance;
            
            if (isNear && !nearbyLocations.Contains(location.name))
            {
                nearbyLocations.Add(location.name);
                webSocketManager?.SendProximityEvent(agentId, "location_near", location.name, transform.position);
            }
            else if (!isNear && nearbyLocations.Contains(location.name))
            {
                nearbyLocations.Remove(location.name);
            }
        }
    }

    private void CheckAgentProximity()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, characterProximityDistance);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent<SphereAIController>(out var otherAgent) && 
                otherAgent.agentId != agentId &&
                !nearbyAgents.Contains(otherAgent.agentId))
            {
                nearbyAgents.Add(otherAgent.agentId);
                webSocketManager?.SendProximityEvent(agentId, "character_near", otherAgent.agentId, transform.position);
            }
        }
        
        // Check for exited agents
        nearbyAgents.RemoveWhere(agentId => 
            !Array.Exists(hitColliders, c => 
                c.TryGetComponent<SphereAIController>(out var a) && a.agentId == agentId));
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

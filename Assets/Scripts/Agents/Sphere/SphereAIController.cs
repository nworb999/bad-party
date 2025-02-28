using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using System.Linq;

[RequireComponent(typeof(SphereMovement))]
public class SphereAIController : MonoBehaviour
{
    [Header("Agent Settings")]
    public string agentId = "sphere_1";

    [Header("Movement Settings")]
    public float moveRadius = 10f; // Maximum distance to move
    public float waitTime = 2f; // Time to wait between movements
    public GameObject centerObject; // Center object for movement radius

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
    private EnvironmentManager environmentManager;
    private GameObject currentTargetLocation;

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
        webSocketManager = FindObjectOfType<WebSocketManager>();
        environmentManager = FindObjectOfType<EnvironmentManager>();

        if (movement == null)
        {
            Debug.LogError("SphereMovement component not found!");
            enabled = false;
            return;
        }

        if (environmentManager == null)
        {
            Debug.LogWarning("EnvironmentManager not found in scene!");
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
        if (environmentManager != null && environmentManager.GetAllLocations().Count > 0)
        {
            // Get a random location from the environment manager
            var locationInfo = environmentManager.GetRandomLocation();
            if (locationInfo != null)
            {
                currentTargetLocation = locationInfo.locationObject;
                movement.MoveToObject(currentTargetLocation);
                
                // Send location reached event
                webSocketManager?.SendLocationReached(agentId, locationInfo.locationName, currentTargetLocation.transform.position);
                return;
            }
        }
        
        // Fallback to random movement if no locations or environment manager
        if (!movement.TryMoveToRandomPoint(centerObject.transform.position, moveRadius, null))
        {
            movement.SetTargetPosition(transform.position);
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
                float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                webSocketManager?.SendProximityEvent(agentId, "item_near", hitCollider.name, distance);
            }
        }
        
        nearbyItems.RemoveWhere(itemId => 
            !Array.Exists(hitColliders, c => c.name == itemId));
    }

    private void CheckLocationProximity()
    {
        if (environmentManager == null) return;
        
        foreach (var location in environmentManager.GetAllLocations())
        {
            if (location.locationObject == null) continue;
            
            float distance = Vector3.Distance(transform.position, location.locationObject.transform.position);
            bool isNear = distance <= locationProximityDistance;
            
            if (isNear && !nearbyLocations.Contains(location.locationName))
            {
                nearbyLocations.Add(location.locationName);
                webSocketManager?.SendProximityEvent(agentId, "location_near", location.locationName, distance);
            }
            else if (!isNear && nearbyLocations.Contains(location.locationName))
            {
                nearbyLocations.Remove(location.locationName);
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
                float distance = Vector3.Distance(transform.position, otherAgent.transform.position);
                webSocketManager?.SendProximityEvent(agentId, "character_near", otherAgent.agentId, distance);
            }
        }
        
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

    public void MoveToNamedLocation(string locationName)
    {
        if (environmentManager == null)
        {
            Debug.LogWarning($"Agent {agentId}: EnvironmentManager not found");
            return;
        }
        
        GameObject targetLocation = environmentManager.GetLocationObject(locationName);
        
        if (targetLocation != null)
        {
            // Stop any waiting coroutine
            isWaiting = false;
            StopAllCoroutines();
            StartCoroutine(ProximityCheckLoop()); // Restart the proximity check
            
            // Set the new destination
            currentTargetLocation = targetLocation;
            movement.MoveToObject(targetLocation);
            ChangeState(MovementState.Walking);
            
            // Send location reached event
            webSocketManager?.SendLocationReached(agentId, locationName, targetLocation.transform.position);
            
        }
        else
        {
            // Try to find a partial match
            var allLocations = environmentManager.GetAllLocations();
            var partialMatch = allLocations.FirstOrDefault(loc => 
                loc.locationName.Contains(locationName) || 
                locationName.Contains(loc.locationName));
                
            if (partialMatch != null && partialMatch.locationObject != null)
            {
                // Same code as above for moving to a location
                isWaiting = false;
                StopAllCoroutines();
                StartCoroutine(ProximityCheckLoop());
                
                currentTargetLocation = partialMatch.locationObject;
                movement.MoveToObject(currentTargetLocation);
                ChangeState(MovementState.Walking);
                
                webSocketManager?.SendLocationReached(agentId, partialMatch.locationName, currentTargetLocation.transform.position);
            }
            else
            {
                Debug.LogWarning($"Agent {agentId}: Location '{locationName}' not found and no partial matches");
            }
        }
    }
}

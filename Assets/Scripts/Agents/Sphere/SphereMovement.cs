using System;
using UnityEngine;

public class SphereMovement : MonoBehaviour
{
    private SphereAIController aiController;

    [Header("Movement Settings")]
    public float moveSpeed = 3f; // Reduced from 5f for slower movement
    public float rotationSpeed = 1.5f; // Reduced for smoother turning
    public float smoothTime = 0.8f; // Increased for smoother acceleration/deceleration
    public float heightAboveGround = 1f;
    public LayerMask groundLayer;
    public float bufferDistance = 0.5f; // Moved from AIController
    public LayerMask obstacleLayer; // Moved from AIController

    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private float currentSpeed;

    private WebSocketManager webSocketManager;

    private void Start()
    {
        AdjustHeightToGround(transform.position);
        currentSpeed = 0f;
        webSocketManager = GameObject.FindObjectOfType<WebSocketManager>();
        aiController = GetComponent<SphereAIController>();
    }

    private void Update()
    {
        Vector3 adjustedTarget = AdjustHeightToGround(targetPosition);
        Vector3 moveDirection = (adjustedTarget - transform.position);
        float distanceToTarget = moveDirection.magnitude;

        float targetSpeed = distanceToTarget > 1f ? moveSpeed : moveSpeed * (distanceToTarget / 1f);
        float dummy = 0f;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref dummy, smoothTime);

        Vector3 newPosition = Vector3.SmoothDamp(
            transform.position,
            adjustedTarget,
            ref currentVelocity,
            smoothTime
        );

        transform.position = AdjustHeightToGround(newPosition);

        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    public void MoveToObject(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        // Get direction from current position to target
        Vector3 directionToTarget = (
            targetObject.transform.position - transform.position
        ).normalized;

        // Calculate combined radius
        float targetRadius = 0f;
        if (targetObject.TryGetComponent<Collider>(out Collider targetCollider))
        {
            if (targetCollider is SphereCollider sphereCollider)
            {
                targetRadius = sphereCollider.radius * targetObject.transform.lossyScale.x;
            }
            else
            {
                targetRadius = targetCollider.bounds.extents.magnitude;
            }
        }

        // Calculate safe distance
        float sphereRadius = GetComponent<SphereCollider>().radius * transform.lossyScale.x;
        float safeDistance = sphereRadius + targetRadius + bufferDistance;

        // Set target point
        Vector3 targetPoint = targetObject.transform.position - (directionToTarget * safeDistance);
        SetTargetPosition(targetPoint);
    }

    public bool TryMoveToRandomPoint(
        Vector3 centerPoint,
        float radius,
        GameObject[] avoidObjects = null
    )
    {
        int maxAttempts = 10;
        float sphereRadius = GetComponent<SphereCollider>().radius * transform.lossyScale.x;

        for (int i = 0; i < maxAttempts; i++)
        {
            float randomAngle = UnityEngine.Random.Range(0f, 360f);
            float randomRadius = radius;

            Vector3 randomDirection = new Vector3(
                Mathf.Cos(randomAngle),
                0f,
                Mathf.Sin(randomAngle)
            ).normalized;

            Vector3 potentialTarget = centerPoint + (randomDirection * randomRadius);
            bool isValidPosition = true;

            // Check against avoid objects
            if (avoidObjects != null)
            {
                foreach (GameObject obj in avoidObjects)
                {
                    if (obj != null && obj.TryGetComponent<Collider>(out Collider objCollider))
                    {
                        float minDistance =
                            sphereRadius + objCollider.bounds.extents.magnitude + bufferDistance;
                        if (Vector3.Distance(potentialTarget, obj.transform.position) < minDistance)
                        {
                            isValidPosition = false;
                            break;
                        }
                    }
                }
            }

            // Check against obstacles
            if (
                isValidPosition
                && Physics.SphereCast(
                    centerPoint,
                    sphereRadius,
                    randomDirection,
                    out RaycastHit hit,
                    randomRadius,
                    obstacleLayer
                )
            )
            {
                randomRadius = hit.distance - bufferDistance;
                if (randomRadius <= 0)
                {
                    isValidPosition = false;
                }
                else
                {
                    potentialTarget = centerPoint + (randomDirection * randomRadius);
                }
            }

            if (isValidPosition)
            {
                SetTargetPosition(potentialTarget);
                return true;
            }
        }

        return false;
    }

    private Vector3 AdjustHeightToGround(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, 20f, groundLayer))
        {
            return new Vector3(position.x, hit.point.y + heightAboveGround, position.z);
        }
        return position;
    }

    public void SetTargetPosition(Vector3 newTarget)
    {
        targetPosition = newTarget;
    }

    public Vector3 GetTargetPosition()
    {
        return targetPosition;
    }

    public bool HasReachedTarget(float threshold = 0.1f)
    {
        Vector3 adjustedTarget = AdjustHeightToGround(targetPosition);
        return Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(adjustedTarget.x, 0, adjustedTarget.z)
            ) < threshold;
    }

    public void StopMovement()
    {
        targetPosition = transform.position;
        currentVelocity = Vector3.zero;
        currentSpeed = 0f;
    }
}

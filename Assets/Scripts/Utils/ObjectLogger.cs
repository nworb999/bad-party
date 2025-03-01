using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectLogger : MonoBehaviour
{
    [SerializeField]
    private uint qsize = 15; // number of messages to keep

    [SerializeField]
    private Vector3 offset = new Vector3(0, 2f, 0); // Offset from object position

    [SerializeField]
    private float fontSize = 12f;

    [SerializeField]
    private bool filterByAgentId = true; // Whether to filter logs by agent ID

    private Queue<string> myLogQueue = new Queue<string>();
    private Camera mainCamera;
    private SphereAIController attachedAgent;
    private string agentId;

    void Start()
    {
        mainCamera = Camera.main;
        
        // Get the attached agent reference
        attachedAgent = GetComponent<SphereAIController>();
        if (attachedAgent != null)
        {
            agentId = attachedAgent.agentId;
        }
        else
        {
            Debug.LogWarning("ObjectLogger: No SphereAIController attached. Will log all messages.");
            filterByAgentId = false;
        }
        
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Only log messages that contain the agent's ID or if filtering is disabled
        if (!filterByAgentId || ShouldShowLog(logString))
        {
            myLogQueue.Enqueue($"{logString}");
            if (type == LogType.Exception)
                myLogQueue.Enqueue($"[{stackTrace}");

            while (myLogQueue.Count > qsize)
                myLogQueue.Dequeue();
        }
    }

    // Determine if a log message should be shown for this agent
    private bool ShouldShowLog(string logString)
    {
        // Filter out regular WebSocket received messages
        if (logString.StartsWith("Received:"))
            return false;
            
        // Special handling for conversation messages
        if (logString.StartsWith("CONVERSATION:"))
        {
            // If this agent is either the speaker or listener, show the message
            return logString.Contains($"{agentId} →") || logString.Contains($"→ {agentId}:");
        }
            
        if (agentId == null) return true;
        
        // Direct mentions of agent ID
        if (logString.Contains(agentId)) return true;
        
        return false;
    }

    void OnGUI()
    {
        if (!mainCamera)
            return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position + offset);

        // Don't show if behind camera
        if (screenPos.z < 0)
            return;

        float y = Screen.height - screenPos.y; // GUI space is inverted in Y

        GUILayout.BeginArea(new Rect(screenPos.x - 250, y, 500, Screen.height));
        GUILayout.Label(
            // Add agent ID as title if we have one
            (agentId != null ? $"Agent: {agentId}\n" : "") + 
            string.Join("\n", myLogQueue.ToArray()),
            new GUIStyle()
            {
                fontSize = (int)fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperCenter,
            }
        );
        GUILayout.EndArea();
    }
}

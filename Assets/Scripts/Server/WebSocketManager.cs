using System;
using UnityEngine;
using NativeWebSocket;
using System.Linq;
using System.Collections.Generic;
using Cinemachine;

public class WebSocketManager : MonoBehaviour
{
    WebSocket websocket;
    private EnvironmentManager environmentManager;

    [System.Serializable]
    public class SetupMessage
    {
        public string messageType = "setup";
        public List<string> agent_ids;
        public List<AreaData> areas = new List<AreaData>();
        public List<string> cameras;
        public List<string> items = new List<string>();
    }

    [System.Serializable]
    public class AreaData
    {
        public string area_name;
        public List<string> locations = new List<string>();
    }

    [System.Serializable]
    public class LocationReachedMessage
    {
        public string messageType = "location_reached";
        public string agent_id;
        public string location_name;
        public float[] coordinates;
    }

    [System.Serializable]
    public class ProximityEventMessage
    {
        public string messageType = "proximity_event";
        public string agent_id;
        public string event_type;
        public string target_id;
        public float distance;
    }

    [System.Serializable]
    public class MoveToLocationMessage
    {
        public string messageType;
        public string agent_id;
        public string location_name;
    }

    async void Start()
    {
        environmentManager = FindObjectOfType<EnvironmentManager>();
        if (environmentManager == null)
        {
            Debug.LogWarning("EnvironmentManager not found in scene!");
        }

        websocket = new WebSocket("ws://localhost:3000/ws");

        websocket.OnOpen += () => 
        {
            Debug.Log("Connection open!");
            SendSetupData();
        };
        websocket.OnError += (e) => Debug.LogError("Error: " + e);
        websocket.OnClose += (e) => Debug.Log("Connection closed!");
        
        websocket.OnMessage += (bytes) =>
        {
            // Convert byte array to string
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received: " + message);
            
            // Check if it's a move command
            if (message.Contains("\"messageType\":\"move_to_location\""))
            {
                try 
                {
                    MoveToLocationMessage moveMessage = JsonUtility.FromJson<MoveToLocationMessage>(message);
                    SphereAIController agent = FindAgentById(moveMessage.agent_id);
                    
                    if (agent != null)
                    {
                        agent.MoveToNamedLocation(moveMessage.location_name);
                        Debug.Log($"Instructed agent {moveMessage.agent_id} to move to {moveMessage.location_name}");
                    }
                    else
                    {
                        Debug.LogWarning($"Agent {moveMessage.agent_id} not found");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing move command: {e.Message}");
                }
            }
        };

        await websocket.Connect();
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
        #endif
    }

    public new async void SendMessage(string message)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(message);
        }
        else
        {
            Debug.LogWarning("Cannot send message: WebSocket is null or not open");
        }
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }

    private void SendSetupData()
    {
        try
        {
            SphereAIController[] agents = FindObjectsOfType<SphereAIController>();
            CinemachineVirtualCamera[] cameras = FindObjectsOfType<CinemachineVirtualCamera>();

            SetupMessage message = new SetupMessage
            {
                agent_ids = agents.Select(agent => agent.agentId).ToList(),
                cameras = cameras
                    .Where(cam => cam != null)
                    .Select(cam => cam.Name)
                    .ToList(),
                items = new List<string>()
            };

            // Get areas and locations from EnvironmentManager
            if (environmentManager != null)
            {
                foreach (var area in environmentManager.GetAllAreas())
                {
                    AreaData areaData = new AreaData
                    {
                        area_name = area.areaName,
                        locations = area.locations.Select(loc => loc.locationName).ToList()
                    };
                    message.areas.Add(areaData);
                }
            }

            string jsonMessage = JsonUtility.ToJson(message);
            Debug.Log($"Sent setup data: {message.agent_ids.Count} agents, " +
                    $"{message.areas.Count} areas, " +
                    $"{message.cameras.Count} cameras");
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending setup data: {e.Message}");
        }
    }

    public void SendLocationReached(string agentId, string locationName, Vector3 position)
    {
        try
        {
            LocationReachedMessage message = new LocationReachedMessage
            {
                agent_id = agentId,
                location_name = locationName,
                coordinates = new float[] { position.x, position.y, position.z }
            };
            
            string jsonMessage = JsonUtility.ToJson(message);
            Debug.Log($"Sent location reached event - Agent: {agentId}, Location: {locationName}");
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending location reached: {e.Message}");
        }
    }

    public void SendProximityEvent(string agentId, string eventType, string targetId, float distance)
    {
        try
        {
            ProximityEventMessage message = new ProximityEventMessage
            {
                agent_id = agentId,
                event_type = eventType,
                target_id = targetId,
                distance = distance
            };
            
            string jsonMessage = JsonUtility.ToJson(message);
            Debug.Log($"Sent proximity event - Agent: {agentId}, Type: {eventType}, Target: {targetId}, Distance: {distance}");
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending proximity event: {e.Message}");
        }
    }

    private SphereAIController FindAgentById(string agentId)
    {
        SphereAIController[] agents = FindObjectsOfType<SphereAIController>();
        return agents.FirstOrDefault(agent => agent.agentId == agentId);
    }
}

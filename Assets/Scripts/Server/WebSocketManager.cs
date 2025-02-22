using System;
using UnityEngine;
using NativeWebSocket;
using System.Linq;
using System.Collections.Generic;
using Cinemachine;

public class WebSocketManager : MonoBehaviour
{
    WebSocket websocket;

    [System.Serializable]
    public class SetupMessage
    {
        public string messageType;
        public string sender;
        public SetupData data;
    }

    [System.Serializable]
    public class SetupData
    {
        public List<string> agent_ids;
        public List<string> locations;
        public List<string> cameras;
        public List<string> items = new List<string>();
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

    async void Start()
    {
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
        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(message);
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
                messageType = "setup",
                sender = "server",
                data = new SetupData
                {
                    agent_ids = agents.Select(agent => agent.agentId).ToList(),
                    locations = agents
                        .SelectMany(agent => agent.locationObjects ?? new GameObject[0])
                        .Where(loc => loc != null)
                        .Select(loc => loc.name)
                        .Distinct()
                        .ToList(),
                    cameras = cameras
                        .Where(cam => cam != null)
                        .Select(cam => cam.Name)
                        .ToList(),
                    items = new List<string>()
                }
            };

            string jsonMessage = JsonUtility.ToJson(message);
            Debug.Log($"Sent setup data: {message.data.agent_ids.Count} agents, " +
                    $"{message.data.locations.Count} locations, " +
                    $"{message.data.cameras.Count} cameras");
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
}

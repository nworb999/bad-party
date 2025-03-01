using System;
using UnityEngine;
using NativeWebSocket;
using System.Linq;
using System.Collections.Generic;
using Cinemachine;
using System.Threading.Tasks;

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
        public List<LocationData> locations = new List<LocationData>();
    }

    [System.Serializable]
    public class LocationData
    {
        public string location_name;
        public float[] coordinates;
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

    [System.Serializable]
    public class ConversationMessage
    {
        public string messageType = "conversation";
        public string speaker_id;
        public string listener_id;
        public string text;
        public float timestamp;
    }

    [System.Serializable]
    public class WaitAtLocationMessage
    {
        public string messageType = "wait_at_location";
        public string agent_id;
        public string location_name;
        public float duration;
    }


    async void Start()
    {
        InitializeEnvironmentManager();
        await InitializeWebSocket();
    }

    private void InitializeEnvironmentManager()
    {
        environmentManager = FindObjectOfType<EnvironmentManager>();
        if (environmentManager == null)
        {
            Debug.LogWarning("EnvironmentManager not found in scene!");
        }
    }

    private async Task InitializeWebSocket()
    {
        websocket = new WebSocket("ws://localhost:3000/ws");
        
        SetupWebSocketEventHandlers();
        
        await websocket.Connect();
    }

    private void SetupWebSocketEventHandlers()
    {
        websocket.OnOpen += () => 
        {
            Debug.Log("Connection open!");
            SendSetupData();
        };
        websocket.OnError += (e) => Debug.LogError("Error: " + e);
        websocket.OnClose += (e) => Debug.Log("Connection closed!");
        websocket.OnMessage += HandleWebSocketMessage;
    }

    private void HandleWebSocketMessage(byte[] bytes)
    {
        // Convert byte array to string
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        Debug.Log("Received: " + message);
        
        // Check message type and handle accordingly
        if (message.Contains("\"messageType\":\"move_to_location\""))
        {
            HandleMoveToLocationMessage(message);
        }
        else if (message.Contains("\"messageType\":\"wait_at_location\""))
        {
            HandleWaitAtLocationMessage(message);
        }
        else if (message.Contains("\"messageType\":\"conversation\""))
        {
            HandleConversationMessage(message);
        }
    }

    private void HandleMoveToLocationMessage(string message)
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

    private void HandleWaitAtLocationMessage(string message)
    {
        try 
        {
            WaitAtLocationMessage waitMessage = JsonUtility.FromJson<WaitAtLocationMessage>(message);
            SphereAIController agent = FindAgentById(waitMessage.agent_id);
            
            if (agent != null)
            {
                // First ensure the agent is at the specified location
                agent.MoveToNamedLocation(waitMessage.location_name);
                
                // Then instruct the agent to wait
                agent.WaitAtCurrentLocation(waitMessage.duration);
                Debug.Log($"Instructed agent {waitMessage.agent_id} to wait at {waitMessage.location_name} for {waitMessage.duration} seconds");
            }
            else
            {
                Debug.LogWarning($"Agent {waitMessage.agent_id} not found");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing wait at location command: {e.Message}");
        }
    }

    private void HandleConversationMessage(string message)
    {
        try 
        {
            ConversationMessage convoMessage = JsonUtility.FromJson<ConversationMessage>(message);
            
            // Find the speaking agent
            SphereAIController speakingAgent = FindAgentById(convoMessage.speaker_id);
            
            if (speakingAgent != null)
            {
                // Have the agent speak the dialogue using the existing Speak method
                speakingAgent.Speak(convoMessage.text);
                
                // Log the conversation in a format that can be captured by ObjectLogger
                Debug.Log($"CONVERSATION: {convoMessage.speaker_id} → {convoMessage.listener_id}: {convoMessage.text}");
            }
            else
            {
                Debug.LogWarning($"Speaking agent {convoMessage.speaker_id} not found");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing conversation: {e.Message}");
        }
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
                    };
                    
                    // Create location data with coordinates
                    foreach (var loc in area.locations)
                    {
                        if (loc.locationObject != null)
                        {
                            Vector3 position = loc.locationObject.transform.position;
                            LocationData locationData = new LocationData
                            {
                                location_name = loc.locationName,
                                coordinates = new float[] { position.x, position.y, position.z }
                            };
                            areaData.locations.Add(locationData);
                        }
                    }
                    
                    message.areas.Add(areaData);
                }
            }

            string jsonMessage = JsonUtility.ToJson(message);
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
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending proximity event: {e.Message}");
        }
    }

    public void LogConversation(string speakerId, string listenerId, string text)
    {
        try
        {
            // Log locally so the ObjectLogger can display it
            Debug.Log($"CONVERSATION: {speakerId} → {listenerId}: {text}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error logging conversation: {e.Message}");
        }
    }

    private SphereAIController FindAgentById(string agentId)
    {
        SphereAIController[] agents = FindObjectsOfType<SphereAIController>();
        return agents.FirstOrDefault(agent => agent.agentId == agentId);
    }
}

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
    }

    [System.Serializable]
    public class StateUpdateMessage
    {
        public string messageType;
        public string sender;
        public string agent_id;
        public object data;
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
                        .ToList()
                }
            };

            string jsonMessage = JsonUtility.ToJson(message);
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending setup data: {e.Message}");
        }
    }

    public void SendStateUpdate(string agentId, object data)
    {
        try
        {
            StateUpdateMessage message = new StateUpdateMessage
            {
                messageType = "state_update",
                sender = "server",
                agent_id = agentId,
                data = data
            };

            string jsonMessage = JsonUtility.ToJson(message);
            SendMessage(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending state update: {e.Message}");
        }
    }
}

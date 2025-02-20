using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;
using System;

public class WebSocketManager : MonoBehaviour 
{
    [SerializeField]
    private string serverUrl = "ws://localhost:8000/ws/py_client";
    
    private WebSocket websocket;
    private bool isConnecting = false;
    private float reconnectDelay = 5f;
    private float reconnectTimer = 0f;
    private string clientId;

    [System.Serializable]
    private class ConnectMessageData
    {
        public string message = "Unity client connection request";
    }

    [System.Serializable]
    private class ConnectMessage
    {
        public string @event = "connect";
        public ConnectMessageData data;
    }

    // Added back state update classes
    [System.Serializable]
    private class WebSocketMessageData
    {
        public string status;
        public string client_id;
    }

    [System.Serializable]
    private class WebSocketMessage
    {
        public string @event;
        public WebSocketMessageData data;
    }

    [System.Serializable]
    private class StateUpdateData
    {
        public string agent_id;
        public string state;
    }

    [System.Serializable]
    private class StateUpdateMessage
    {
        public string @event = "state_update";
        public string client_id;
        public StateUpdateData data;
    }

    private async void Start()
    {
        Debug.Log("WebSocketManager starting...");
        await ConnectToServer();
    }

    private async Task ConnectToServer()
    {
        if (isConnecting)
        {
            Debug.Log("Already attempting to connect...");
            return;
        }
        
        isConnecting = true;
        Debug.Log($"Attempting to connect to: {serverUrl}");

        try
        {
            Debug.Log($"Attempting to connect to {serverUrl}");
            websocket = new WebSocket(serverUrl);

            websocket.OnOpen += () =>
            {
                Debug.Log("Connection established!");
                SendConnectMessage();
            };

            websocket.OnError += (e) =>
            {
                Debug.LogError($"WebSocket error: {e}");
                Debug.LogError($"WebSocket state: {websocket.State}");
            };

            websocket.OnClose += (code) =>
            {
                Debug.Log($"Connection closed with code: {code}");
                Debug.Log($"WebSocket state at close: {websocket.State}");
                isConnecting = false;
                reconnectTimer = reconnectDelay;
            };

            websocket.OnMessage += (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log($"Received message: {message}");
                HandleServerMessage(message);
            };

            Debug.Log("Initiating WebSocket connection...");
            await websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception during connection attempt: {e}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            isConnecting = false;
            reconnectTimer = reconnectDelay;
        }
    }

    private void HandleServerMessage(string message)
    {
        try
        {
            var data = JsonUtility.FromJson<WebSocketMessage>(message);
            if (data.@event == "connect" && data.data.status == "granted")
            {
                clientId = data.data.client_id;
                Debug.Log($"Connected with client ID: {clientId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error handling message: {e}");
        }
    }

    public void SendStateUpdate(string agentId, object state)
    {
        if (websocket?.State == WebSocketState.Open && !string.IsNullOrEmpty(clientId))
        {
            var stateData = new StateUpdateData
            {
                agent_id = agentId,
                state = JsonUtility.ToJson(state)
            };

            var stateMessage = new StateUpdateMessage
            {
                client_id = clientId,
                data = stateData
            };

            string json = JsonUtility.ToJson(stateMessage);
            websocket.SendText(json);
        }
    }

    private void SendConnectMessage()
    {
        try
        {
            var messageData = new ConnectMessageData { message = "Unity client connection request" };
            var connectMessage = new ConnectMessage { data = messageData };
            string json = JsonUtility.ToJson(connectMessage);
            Debug.Log($"Sending connect message: {json}");
            websocket.SendText(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error sending connect message: {e}");
        }
    }

    private void Update()
    {
        if (websocket == null || websocket.State == WebSocketState.Closed)
        {
            reconnectTimer -= Time.deltaTime;
            if (reconnectTimer <= 0)
            {
                _ = ConnectToServer();
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            Debug.Log("Closing WebSocket connection...");
            await websocket.Close();
        }
    }
} 
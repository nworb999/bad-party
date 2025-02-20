using System;
using UnityEngine;
using NativeWebSocket;

public class WebSocketBasic : MonoBehaviour
{
    WebSocket websocket;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:3000/ws");

        websocket.OnOpen += () => Debug.Log("Connection open!");
        websocket.OnError += (e) => Debug.LogError("Error: " + e);
        websocket.OnClose += (e) => Debug.Log("Connection closed!");
        
        websocket.OnMessage += (bytes) =>
        {
            // Convert byte array to string
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received: " + message);
        };

        await websocket.Connect();
        
        // Send initial connection message
        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("Unity client connected!");
        }
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
        #endif
    }

    public async void SendMessage(string message)
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
}

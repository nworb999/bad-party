using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkServer : MonoBehaviour
{
    // TODO -remove 
    [Serializable]
    private class StateData
    {
        public string state;
    }

    [Serializable]
    private class AgentUpdate
    {
        public string objective;
        public string thought;
        public string emotion;
        public string current_action;
        public string current_animation;
    }

    [Serializable]
    private class EventWrapper
    {
        public string type;
        public string agent_id;
        public string data;
    }

    [Header("Network Settings")]
    public int tcpPort = 8052; // Port for TCP (requests)
    public int udpPort = 8053; // Port for UDP (events)
    public string pythonServerIP = "172.29.61.180";
    public int pythonServerPort = 8001; // UDP port where Python server listens
    private UdpClient udpReceiver;
    private Thread udpReceiverThread;

    private TcpListener tcpListener;
    private UdpClient udpClient;
    private Thread tcpListenerThread;
    private Thread udpSenderThread;
    private bool isRunning = false;
    private ConcurrentQueue<string> eventQueue = new ConcurrentQueue<string>();
    private readonly object tcpLock = new object();

    private void Start()
    {
        StartServers();
    }

    private void StartServers()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        Debug.Log("Starting TCP and UDP servers...");

        try
        {
            // TCP Setup
            tcpListenerThread = new Thread(new ThreadStart(TCPListenerThread));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();

            // UDP Sender Setup
            Debug.Log($"Initializing UDP sender to Python server at {pythonServerIP}:{pythonServerPort}");
            udpClient = new UdpClient();
            udpSenderThread = new Thread(new ThreadStart(UDPSenderThread));
            udpSenderThread.IsBackground = true;
            udpSenderThread.Start();

            // UDP Receiver Setup
            Debug.Log($"Initializing UDP receiver on port {udpPort}");
            try {
                udpReceiver = new UdpClient(udpPort);
                Debug.Log($"UDP receiver successfully bound to port {udpPort}");
                udpReceiverThread = new Thread(new ThreadStart(UDPReceiverThread));
                udpReceiverThread.IsBackground = true;
                udpReceiverThread.Start();
                Debug.Log("UDP receiver thread started");
            }
            catch (SocketException se) {
                Debug.LogError($"Failed to bind UDP receiver to port {udpPort}: {se.Message}");
                throw;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting servers: {e.Message}\nStackTrace: {e.StackTrace}");
            StopServers();
        }
    }

    private void UDPReceiverThread()
    {
        Debug.Log("[UDP] Receiver thread starting...");
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        try 
        {
            Debug.Log("[UDP] Starting receive loop...");
            while (isRunning)
            {
                try
                {
                    Debug.Log("[UDP] Waiting for next message...");
                    byte[] data = udpReceiver.Receive(ref remoteEndPoint);
                    Debug.Log($"[UDP] Received {data.Length} bytes from {remoteEndPoint}");
                    
                    string message = Encoding.UTF8.GetString(data);
                    Debug.Log($"[UDP] Raw message: {message}");

                    try {
                        EventWrapper wrapper = JsonUtility.FromJson<EventWrapper>(message);
                        Debug.Log($"[UDP] Event type: {wrapper.type}, Agent: {wrapper.agent_id}");
                        
                        if (wrapper.type == "agent_update")
                        {
                            AgentUpdate update = JsonUtility.FromJson<AgentUpdate>(wrapper.data);
                            Debug.Log($"[UDP] Agent {wrapper.agent_id} update processed successfully");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError($"[UDP] JSON parsing error: {parseEx.Message}\nRaw message was: {message}");
                    }
                }
                catch (SocketException se)
                {
                    Debug.LogError($"[UDP] Socket error in receive loop: {se.Message} (ErrorCode: {se.SocketErrorCode})");
                    if (!isRunning) break;
                    Thread.Sleep(1000); // Wait a bit before retrying
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"[UDP] Receive loop error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                        Thread.Sleep(1000); // Wait a bit before retrying
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDP] Fatal error in receiver thread: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
        finally 
        {
            Debug.Log("[UDP] Receiver thread ending");
        }
    }


    // private void UDPReceiverThread()
    // {
    //     IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
    //     while (isRunning)
    //     {
    //         try
    //         {
    //             byte[] data = udpReceiver.Receive(ref remoteEndPoint);
    //             string message = Encoding.UTF8.GetString(data);
    //             Debug.Log($"[UDP] Received raw message: {message}"); // Added raw message logging

    //             try {
    //                 // Parse the agent update
    //                 EventWrapper wrapper = JsonUtility.FromJson<EventWrapper>(message);
    //                 Debug.Log($"[UDP] Parsed event type: {wrapper.type}, agent: {wrapper.agent_id}"); // Added parsing debug
                    
    //                 if (wrapper.type == "agent_update")
    //                 {
    //                     AgentUpdate update = JsonUtility.FromJson<AgentUpdate>(wrapper.data);
    //                     Debug.Log($"Agent {wrapper.agent_id} update:");
    //                     Debug.Log($"  Objective: {update.objective}");
    //                     Debug.Log($"  Thought: {update.thought}");
    //                     Debug.Log($"  Emotion: {update.emotion}");
    //                     Debug.Log($"  Action: {update.current_action}");
    //                     Debug.Log($"  Animation: {update.current_animation}");
                        
    //                     // Handle the agent update here - update UI, agent state, etc.
    //                 }
    //             }
    //             catch (Exception parseEx)
    //             {
    //                 Debug.LogError($"Error parsing UDP message: {parseEx.Message}\nMessage was: {message}");
    //             }
    //         }
    //         catch (Exception e)
    //         {
    //             if (isRunning)
    //             {
    //                 Debug.LogError($"UDP Receive error: {e.Message}\nStackTrace: {e.StackTrace}");
    //             }
    //         }
    //     }
    // }

    private void TCPListenerThread()
    {
        try
        {
            lock (tcpLock)
            {
                Debug.Log($"Creating TCP listener on port {tcpPort}...");
                tcpListener = new TcpListener(IPAddress.Any, tcpPort);
                tcpListener.Start();
                Debug.Log($"TCP Server started successfully on port {tcpPort}");
            }

            while (isRunning)
            {
                try
                {
                    Debug.Log("Waiting for TCP client connection...");
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Debug.Log($"TCP Client connected from {client.Client.RemoteEndPoint}");

                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleTCPClient));
                    clientThread.IsBackground = true;
                    clientThread.Start(client);
                    Debug.Log("Started new client handler thread");
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                {
                    Debug.Log("TCP listener interrupted - shutting down normally");
                    break;
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError(
                            $"TCP Server error: {e.Message}\nStackTrace: {e.StackTrace}"
                        );
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (isRunning)
            {
                Debug.LogError($"TCP Server fatal error: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }
        finally
        {
            Debug.Log("TCP Listener Thread ending, stopping TCP listener");
            StopTCPListener();
        }
    }

    private void HandleTCPClient(object obj)
    {
        using (TcpClient client = (TcpClient)obj)
        using (NetworkStream stream = client.GetStream())
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Debug.Log($"[TCP] Received request: {request}");

                string response = HandleRequest(request);

                Debug.Log($"[TCP] Sending response: {response}");

                byte[] responseData = Encoding.UTF8.GetBytes(response);
                stream.Write(responseData, 0, responseData.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling client: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }
    }

    private void UDPSenderThread()
    {
        while (isRunning)
        {
            if (eventQueue.TryDequeue(out string eventData))
            {
                try
                {
                    // Debug.Log($"[UDP] Sending event: {eventData}");
                    byte[] data = Encoding.UTF8.GetBytes(eventData);
                    udpClient.Send(data, data.Length, pythonServerIP, pythonServerPort);
                    // Debug.Log($"[UDP] Successfully sent {data.Length} bytes");
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"UDP Send error: {e.Message}\nStackTrace: {e.StackTrace}");
                    }
                }
            }
            Thread.Sleep(10);
        }
    }

    private void StopTCPListener()
    {
        lock (tcpLock)
        {
            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                    Debug.Log("TCP listener stopped successfully");
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"Error stopping TCP listener: {e.Message}\nStackTrace: {e.StackTrace}"
                    );
                }
                tcpListener = null;
            }
        }
    }

    private void StopServers()
    {
        isRunning = false;

        StopTCPListener();

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                Debug.Log("UDP client closed successfully");
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"Error closing UDP client: {e.Message}\nStackTrace: {e.StackTrace}"
                );
            }
            udpClient = null;
        }

        if (udpReceiver != null)
        {
            try
            {
                udpReceiver.Close();
                Debug.Log("UDP receiver closed successfully");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing UDP receiver: {e.Message}\nStackTrace: {e.StackTrace}");
            }
            udpReceiver = null;
        }
        Debug.Log("All servers stopped");
    }

    private string HandleRequest(string request)
    {
        Debug.Log($"HandleRequest called with request: {request}");
        try
        {
            string response;
            switch (request)
            {
                case "get_position":
                    Vector3 pos = transform.position;
                    response = JsonUtility.ToJson(
                        new
                        {
                            x = pos.x,
                            y = pos.y,
                            z = pos.z,
                        }
                    );
                    Debug.Log($"Handling get_position request, response: {response}");
                    return response;
                case "get_state":
                    response = JsonUtility.ToJson(new { state = "active" });
                    Debug.Log($"Handling get_state request, response: {response}");
                    return response;
                default:
                    response = JsonUtility.ToJson(new { error = "Unknown request" });
                    Debug.Log($"Unknown request type, response: {response}");
                    return response;
            }
        }
        catch (Exception e)
        {
            string errorResponse = JsonUtility.ToJson(new { error = e.Message });
            Debug.LogError($"Error handling request: {e.Message}\nStackTrace: {e.StackTrace}");
            return errorResponse;
        }
    }

    public void SendEvent(string eventType, string agentId, object eventData)
    {
        if (!isRunning)
        {
            Debug.Log("Server not running, ignoring event");
            return;
        }

        // If it's a position update, convert to proper serializable format
        if (eventType == "position_update" || eventType == "state_change" || eventType == "destination_change")
        {
            var wrapper = new EventWrapper
            {
                type = eventType,
                agent_id = agentId,
                data = JsonUtility.ToJson(eventData),
            };

            string jsonEvent = JsonUtility.ToJson(wrapper);
            eventQueue.Enqueue(jsonEvent);
            // Debug.Log($"Event queued: {jsonEvent}");

            if (eventType == "state_change")
            {
                var stateData = JsonUtility.FromJson<StateData>(JsonUtility.ToJson(eventData));
                string unityState = stateData.state.ToLower();
                
                // Create mock agent update based on state
                var agentUpdate = new AgentUpdate
                {
                    objective = unityState == "walking" ? "Moving to new location" : "Taking a moment",
                    thought = unityState == "walking" ? "Making my way through the space" : "Just another moment in the day",
                    emotion = unityState == "walking" ? "active" : "idle",
                    current_action = unityState,
                    current_animation = unityState
                };

                var updateWrapper = new EventWrapper
                {
                    type = "agent_update",
                    agent_id = agentId,
                    data = JsonUtility.ToJson(agentUpdate)
                };

                string updateEvent = JsonUtility.ToJson(updateWrapper);
                eventQueue.Enqueue(updateEvent);
                Debug.Log($"{(unityState == "walking" ? "Making my way through the space" : "Just another moment in the day")}");
            }
        }
        else
        {
            Debug.LogError($"Unsupported event type or data format: {eventType}");
        }
    }

    private void OnDestroy()
    {
        StopServers();
    }

    private void OnApplicationQuit()
    {
        StopServers();
    }
}

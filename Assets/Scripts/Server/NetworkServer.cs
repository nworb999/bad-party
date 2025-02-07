using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System;



public class NetworkServer : MonoBehaviour
{
    [Serializable]
    private class EventWrapper
    {
        public string type;
        public string data;
    }

    [Header("Network Settings")]
    public int tcpPort = 8052;        // Port for TCP (requests)
    public int udpPort = 8053;        // Port for UDP (events)
    public string pythonServerIP = "172.29.61.180";
    public int pythonServerPort = 8001;  // UDP port where Python server listens

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
            tcpListenerThread = new Thread(new ThreadStart(TCPListenerThread));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();

            udpClient = new UdpClient();
            udpSenderThread = new Thread(new ThreadStart(UDPSenderThread));
            udpSenderThread.IsBackground = true;
            udpSenderThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting servers: {e.Message}\nStackTrace: {e.StackTrace}");
            StopServers();
        }
    }

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
                        Debug.LogError($"TCP Server error: {e.Message}\nStackTrace: {e.StackTrace}");
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
                    Debug.LogWarning($"Error stopping TCP listener: {e.Message}\nStackTrace: {e.StackTrace}");
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
                Debug.LogWarning($"Error closing UDP client: {e.Message}\nStackTrace: {e.StackTrace}");
            }
            udpClient = null;
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
                    response = JsonUtility.ToJson(new { x = pos.x, y = pos.y, z = pos.z });
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

    public void SendEvent(string eventType, object eventData)
    {
        if (!isRunning)
        {
            Debug.Log("Server not running, ignoring event");
            return;
        }

        // If it's a position update, convert to proper serializable format
        if (eventType == "position_update" || eventType == "state_change")
        {
            var wrapper = new EventWrapper
            {
                type = eventType,
                data = JsonUtility.ToJson(eventData)
            };
            
            string jsonEvent = JsonUtility.ToJson(wrapper);
            eventQueue.Enqueue(jsonEvent);
            // Debug.Log($"Event queued: {jsonEvent}");
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
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System;

public class NetworkServer : MonoBehaviour
{
    [Header("Network Settings")]
    public int tcpPort = 8052;        // Port for TCP (requests)
    public int udpPort = 8053;        // Port for UDP (events)
    public string pythonServerIP = "127.0.0.1";
    public int pythonServerPort = 8054;  // UDP port where Python server listens

    private TcpListener tcpListener;
    private UdpClient udpClient;
    private Thread tcpListenerThread;
    private Thread udpSenderThread;
    private bool isRunning = false;
    private ConcurrentQueue<string> eventQueue = new ConcurrentQueue<string>();

    private void Start()
    {
        isRunning = true;
        StartServers();
    }

    private void StartServers()
    {
        // Start TCP listener for incoming requests
        tcpListenerThread = new Thread(new ThreadStart(TCPListenerThread));
        tcpListenerThread.IsBackground = true;
        tcpListenerThread.Start();

        // Start UDP client for sending events
        udpClient = new UdpClient();
        udpSenderThread = new Thread(new ThreadStart(UDPSenderThread));
        udpSenderThread.IsBackground = true;
        udpSenderThread.Start();
    }

    private void TCPListenerThread()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            tcpListener.Start();
            Debug.Log($"TCP Server started on port {tcpPort}");

            while (isRunning)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleTCPClient));
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"TCP Server error: {e.Message}");
        }
    }

    private void HandleTCPClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        try
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            // Handle request and prepare response
            string response = HandleRequest(request);
            
            // Send response
            byte[] responseData = Encoding.UTF8.GetBytes(response);
            stream.Write(responseData, 0, responseData.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling client: {e.Message}");
        }
        finally
        {
            client.Close();
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
                    byte[] data = Encoding.UTF8.GetBytes(eventData);
                    udpClient.Send(data, data.Length, pythonServerIP, pythonServerPort);
                }
                catch (Exception e)
                {
                    Debug.LogError($"UDP Send error: {e.Message}");
                }
            }
            Thread.Sleep(10); // Prevent tight loop
        }
    }

    private string HandleRequest(string request)
    {
        // TODO: Implement your request handling logic here
        // Example:
        try
        {
            switch (request)
            {
                case "get_position":
                    Vector3 pos = transform.position;
                    return JsonUtility.ToJson(new { x = pos.x, y = pos.y, z = pos.z });
                case "get_state":
                    return JsonUtility.ToJson(new { state = "active" });
                default:
                    return JsonUtility.ToJson(new { error = "Unknown request" });
            }
        }
        catch (Exception e)
        {
            return JsonUtility.ToJson(new { error = e.Message });
        }
    }

    // Call this method to send events to the Python server
    public void SendEvent(string eventType, object eventData)
    {
        string jsonEvent = JsonUtility.ToJson(new { type = eventType, data = eventData });
        eventQueue.Enqueue(jsonEvent);
    }

    private void OnDestroy()
    {
        isRunning = false;
        tcpListener?.Stop();
        udpClient?.Close();
    }
}
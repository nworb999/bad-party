using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine; 

[Serializable]
public class NetworkMessage<T>
{
    public string eventType;
    public string agentId;
    public T data; 
    public NetworkMessage(string eventType, string agentId, T data)
    {
        this.eventType = eventType;
        this.agentId = agentId;
        this.data = data;
    }
}

public class NetworkServer : MonoBehaviour 
{
    private const string MESSAGE_DELIMITER = "<END>";
    
    private TcpListener server;
    private TcpClient pythonClient;
    private NetworkStream stream;
    private Thread listenerThread;
    private bool isRunning = true;

    private void Start()
    {
        // Start the server on port 5000
        server = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
        server.Start();
        Debug.Log("Unity Server started on port 5000");

        // Start listening for connections in a separate thread
        listenerThread = new Thread(new ThreadStart(ListenForConnections));
        listenerThread.Start();
    }

    private void ListenForConnections()
    {
        try
        {
            // Wait for Python client to connect
            pythonClient = server.AcceptTcpClient();
            Debug.Log("Python client connected!");
            stream = pythonClient.GetStream();

            while (isRunning)
            {
                // Check if there's data to read
                if (pythonClient.Available > 0)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Debug.Log($"Received from Python: {message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
        }
    }

    public void SendUpdate(string message)
    {
        if (stream != null && pythonClient != null && pythonClient.Connected)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
            
            // Send length first, then message
            stream.Write(lengthPrefix, 0, 4);
            stream.Write(messageBytes, 0, messageBytes.Length);
        }
    }

    private void OnDestroy()
    {
        // Clean up
        isRunning = false;
        if (listenerThread != null) listenerThread.Abort();
        if (stream != null) stream.Close();
        if (pythonClient != null) pythonClient.Close();
        if (server != null) server.Stop();
    }
}
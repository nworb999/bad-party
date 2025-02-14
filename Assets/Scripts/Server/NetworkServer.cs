using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkServer : MonoBehaviour 
{
    private const string MESSAGE_DELIMITER = "<END>";
    
    private TcpListener server;
    private TcpClient pythonClient;
    private NetworkStream stream;
    private Thread listenerThread;
    private bool isRunning = true;
    private bool needsSetupData = false;

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

    private void Update()
    {
        // Handle setup data sending on the main thread
        if (needsSetupData)
        {
            SendSetupData();
            needsSetupData = false;
        }
    }

    private void ListenForConnections()
    {
        try
        {
            // Wait for Python client to connect
            pythonClient = server.AcceptTcpClient();
            Debug.Log("Python client connected!");
            stream = pythonClient.GetStream();

            // Signal that we need to send setup data
            needsSetupData = true;

            while (isRunning)
            {
                // Check if there's data to read
                if (pythonClient.Available > 0)
                {
                    // Read the length of the message
                    byte[] lengthBuffer = new byte[4];
                    int bytesRead = stream.Read(lengthBuffer, 0, 4);
                    if (bytesRead != 4)
                    {
                        Debug.LogError("Failed to read message length. Disconnecting.");
                        break;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Read the message
                    byte[] buffer = new byte[messageLength];
                    int totalBytesRead = 0;
                    while (totalBytesRead < messageLength)
                    {
                        bytesRead = stream.Read(buffer, totalBytesRead, messageLength - totalBytesRead);
                        if (bytesRead == 0)
                        {
                            Debug.LogError("Client disconnected while reading message.");
                            break;
                        }
                        totalBytesRead += bytesRead;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);
                    Debug.Log($"Received from Python: {message}");
                }

                // Add a small delay to prevent excessive CPU usage
                Thread.Sleep(10);
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
            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                
                // Send length first, then message
                stream.Write(lengthPrefix, 0, 4);
                stream.Write(messageBytes, 0, messageBytes.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending update: {e.Message}");
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up
        isRunning = false;
        if (listenerThread != null) 
        {
            listenerThread.Abort();
            listenerThread = null;
        }
        if (stream != null)
        {
            stream.Close();
            stream = null;
        }
        if (pythonClient != null)
        {
            pythonClient.Close();
            pythonClient = null;
        }
        if (server != null)
        {
            server.Stop();
            server = null;
        }
    }

    private void SendSetupData()
    {
        Debug.Log("Sending setup data");
        try
        {
            // Safe to call FindObjectsOfType here since we're on the main thread
            SphereAIController[] agents = FindObjectsOfType<SphereAIController>();

            // Extract agent IDs
            List<string> agentIds = agents.Select(agent => agent.agentId).ToList();

            // Extract unique location names from all agents
            HashSet<string> locationNames = new HashSet<string>();
            foreach (var agent in agents)
            {
                if (agent.locationObjects != null)
                {
                    foreach (var location in agent.locationObjects)
                    {
                        if (location != null)
                        {
                            locationNames.Add(location.name);
                        }
                    }
                }
            }

            // Create the setup data object
            var setupData = new
            {
                agent_ids = agentIds,
                locations = locationNames.ToList()
            };

            // Convert setup data to JSON
            string setupJson = JsonUtility.ToJson(setupData);

            // Create the setup message
            string setupMessage = $"setup|server|{setupJson}";

            // Send the setup message
            SendUpdate(setupMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending setup data: {e.Message}");
        }
    }
}
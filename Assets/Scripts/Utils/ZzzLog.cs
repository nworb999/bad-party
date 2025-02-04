using UnityEngine;
using System.Collections;
using System;

public class ZzzLog : MonoBehaviour
{
    uint qsize = 15;  // number of messages to keep
    Queue myLogQueue = new Queue();

    void Start() {
        Debug.Log("Started up logging.");
    }

    void OnEnable() {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type) {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logLevel = type.ToString().ToUpper().PadRight(7);
        
        myLogQueue.Enqueue($"[{timestamp}] [{logLevel}] {logString}");
        if (type == LogType.Exception)
            myLogQueue.Enqueue($"[{timestamp}] [TRACE  ] {stackTrace}");
            
        while (myLogQueue.Count > qsize)
            myLogQueue.Dequeue();
    }

    void OnGUI() {
        GUILayout.BeginArea(new Rect(Screen.width - 500, 0, 500, Screen.height));
        GUILayout.Label("\n" + string.Join("\n", myLogQueue.ToArray()), new GUIStyle() {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        });
        GUILayout.EndArea();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectLogger : MonoBehaviour
{
    [SerializeField]
    private uint qsize = 15; // number of messages to keep

    [SerializeField]
    private Vector3 offset = new Vector3(0, 2f, 0); // Offset from object position

    [SerializeField]
    private float fontSize = 12f;

    private Queue<string> myLogQueue = new Queue<string>();
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        Application.logMessageReceived += HandleLog;

    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {

        myLogQueue.Enqueue($"{logString}");
        if (type == LogType.Exception)
            myLogQueue.Enqueue($"[{stackTrace}");

        while (myLogQueue.Count > qsize)
            myLogQueue.Dequeue();
    }

    void OnGUI()
    {
        if (!mainCamera)
            return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position + offset);

        // Don't show if behind camera
        if (screenPos.z < 0)
            return;

        float y = Screen.height - screenPos.y; // GUI space is inverted in Y

        GUILayout.BeginArea(new Rect(screenPos.x - 250, y, 500, Screen.height));
        GUILayout.Label(
            "\n" + string.Join("\n", myLogQueue.ToArray()),
            new GUIStyle()
            {
                fontSize = (int)fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperCenter,
            }
        );
        GUILayout.EndArea();
    }
}

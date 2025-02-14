using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;

    private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                // Find existing dispatcher or create new one
                _instance = FindObjectOfType<UnityMainThreadDispatcher>();

                if (_instance == null)
                {
                    GameObject dispatcherGameObject = new GameObject("UnityMainThreadDispatcher");
                    _instance = dispatcherGameObject.AddComponent<UnityMainThreadDispatcher>();
                }

                DontDestroyOnLoad(_instance.gameObject); // Optional: Prevent destruction on scene changes
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject); // Optional: Prevent destruction on scene changes
        }
        else if (_instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
    }

    private void Update()
    {
        while (_executionQueue.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    public static void Enqueue(Action action)
    {
        if (action == null) throw new ArgumentNullException("action");
        Instance._executionQueue.Enqueue(action);
    }

    public static void Enqueue(IEnumerator action)
    {
        if (action == null) throw new ArgumentNullException("action");
        Enqueue(() => Instance.StartCoroutine(action));
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
} 
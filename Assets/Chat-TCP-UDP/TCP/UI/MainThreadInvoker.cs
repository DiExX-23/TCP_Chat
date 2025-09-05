using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ensure actions enqueued from background threads execute on Unity's main thread.
/// Network callbacks should call MainThreadInvoker.Enqueue(() => { /* UI changes */ });
/// Place this script in Assets/Chat-TCP-UDP/TCP/UI/ and attach it to a GameObject named "MainThreadInvoker".
/// </summary>
public class MainThreadInvoker : MonoBehaviour
{
    private static readonly Queue<Action> queue = new Queue<Action>();
    private static MainThreadInvoker instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Enqueue an action to run on the main thread.</summary>
    public static void Enqueue(Action action)
    {
        if (action == null) return;
        lock (queue) { queue.Enqueue(action); }
    }

    private void Update()
    {
        if (queue.Count == 0) return;
        Action[] actions;
        lock (queue)
        {
            actions = queue.ToArray();
            queue.Clear();
        }

        foreach (var a in actions)
        {
            try { a?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
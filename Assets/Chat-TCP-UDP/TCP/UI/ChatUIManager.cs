using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ChatUIManager: handles instantiating incoming/outgoing message prefabs under a ScrollRect
/// and auto-scrolls to the bottom when a new message arrives.
/// </summary>
public class ChatUIManager : MonoBehaviour
{
    [Header("Assign in Inspector (Scene objects)")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform messageContainer;
    [SerializeField] private GameObject incomingMessagePrefab;
    [SerializeField] private GameObject outgoingMessagePrefab;

    public void AddIncoming(string text)
    {
        InstantiateMessage(incomingMessagePrefab, text);
        ScrollToBottom();
    }

    public void AddOutgoing(string text)
    {
        InstantiateMessage(outgoingMessagePrefab, text);
        ScrollToBottom();
    }

    private void InstantiateMessage(GameObject prefab, string text)
    {
        if (prefab == null || messageContainer == null) return;

        var go = Instantiate(prefab, messageContainer);

        // Prefer TextMeshPro if available
        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        // Fallback to legacy UI Text
        var legacy = go.GetComponentInChildren<Text>();
        if (legacy != null)
        {
            legacy.text = text;
        }
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }
}
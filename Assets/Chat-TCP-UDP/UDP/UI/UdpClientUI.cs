using System.Net;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI bridge that accepts GameObject references for TMP InputFields and Buttons,
/// resolves components at runtime and controls the StreamingManager start/stop.
/// </summary>
public class UdpClientUI : MonoBehaviour
{
    // assign GameObject that contains the TMP/IP InputField in the scene
    public GameObject ipInputObject;
    // assign GameObject that contains the TMP/Port InputField in the scene
    public GameObject portInputObject;
    // assign GameObject that contains the Start Button in the scene
    public GameObject startButtonObject;
    // assign GameObject that contains the Stop Button in the scene
    public GameObject stopButtonObject;
    // assign GameObject that holds the StreamingManager (client or server) in the scene
    public GameObject streamingManagerObject;

    // resolved input components (TextMeshPro or legacy)
    private TMP_InputField ipTMP;
    private TMP_InputField portTMP;
    private InputField ipLegacy;
    private InputField portLegacy;

    // resolved buttons
    private Button startButton;
    private Button stopButton;

    // resolved managers (either may be present)
    private StreamingManager clientManager;
    private StreamingManagerServer serverManager;

    // resolve components and hook events
    private void Awake()
    {
        // resolve IP input component (TMP preferred)
        if (ipInputObject != null)
        {
            ipTMP = ipInputObject.GetComponent<TMP_InputField>();
            if (ipTMP == null) ipLegacy = ipInputObject.GetComponent<InputField>();
        }

        // resolve Port input component (TMP preferred)
        if (portInputObject != null)
        {
            portTMP = portInputObject.GetComponent<TMP_InputField>();
            if (portTMP == null) portLegacy = portInputObject.GetComponent<InputField>();
        }

        // resolve Buttons
        if (startButtonObject != null) startButton = startButtonObject.GetComponent<Button>();
        if (stopButtonObject != null) stopButton = stopButtonObject.GetComponent<Button>();

        // resolve StreamingManager component(s) from provided GameObject
        if (streamingManagerObject != null)
        {
            clientManager = streamingManagerObject.GetComponent<StreamingManager>();
            serverManager = streamingManagerObject.GetComponent<StreamingManagerServer>();
        }

        // register input callbacks (TMP and legacy types)
        if (ipTMP != null) ipTMP.onEndEdit.AddListener(OnIpEndEdit);
        else if (ipLegacy != null) ipLegacy.onEndEdit.AddListener(OnIpEndEdit);

        if (portTMP != null) portTMP.onEndEdit.AddListener(OnPortEndEdit);
        else if (portLegacy != null) portLegacy.onEndEdit.AddListener(OnPortEndEdit);

        // register button callbacks
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
    }

    // unhook events to avoid leaks
    private void OnDestroy()
    {
        if (ipTMP != null) ipTMP.onEndEdit.RemoveListener(OnIpEndEdit);
        if (ipLegacy != null) ipLegacy.onEndEdit.RemoveListener(OnIpEndEdit);

        if (portTMP != null) portTMP.onEndEdit.RemoveListener(OnPortEndEdit);
        if (portLegacy != null) portLegacy.onEndEdit.RemoveListener(OnPortEndEdit);

        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
    }

    // Called when IP input finishes editing
    private void OnIpEndEdit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return; // ignore empty
        string trimmed = text.Trim();
        // accept numeric IP or hostname
        if (IPAddress.TryParse(trimmed, out _))
        {
            // apply to whichever manager is present
            if (clientManager != null) clientManager.SetIP(trimmed);
            if (serverManager != null) serverManager.SetIP(trimmed);
            Debug.Log($"[UdpClientUI] Numeric IP set: {trimmed}");
        }
        else
        {
            // accept hostname strings as well
            if (clientManager != null) clientManager.SetIP(trimmed);
            if (serverManager != null) serverManager.SetIP(trimmed);
            Debug.LogWarning($"[UdpClientUI] IP not numeric, using as hostname: {trimmed}");
        }
    }

    // Called when Port input finishes editing
    private void OnPortEndEdit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return; // ignore empty
        string trimmed = text.Trim();
        if (int.TryParse(trimmed, out int port) && port >= 0 && port <= 65535)
        {
            if (clientManager != null) clientManager.SetPort(trimmed);
            if (serverManager != null) serverManager.SetPort(trimmed);
            Debug.Log($"[UdpClientUI] Port set: {port}");
        }
        else
        {
            Debug.LogWarning($"[UdpClientUI] Invalid port entered: {trimmed}");
        }
    }

    // Called when Start button clicked
    private void OnStartClicked()
    {
        // ensure latest values from inputs are applied before starting
        string ipText = ipTMP != null ? ipTMP.text : (ipLegacy != null ? ipLegacy.text : null);
        string portText = portTMP != null ? portTMP.text : (portLegacy != null ? portLegacy.text : null);

        if (!string.IsNullOrWhiteSpace(ipText)) OnIpEndEdit(ipText);
        if (!string.IsNullOrWhiteSpace(portText)) OnPortEndEdit(portText);

        // start the appropriate manager
        if (clientManager != null) clientManager.StartStreaming();
        else if (serverManager != null) serverManager.StartStreaming();
        else Debug.LogWarning("[UdpClientUI] No StreamingManager component found on assigned GameObject.");
    }

    // Called when Stop button clicked
    private void OnStopClicked()
    {
        if (clientManager != null) clientManager.StopStreaming();
        if (serverManager != null) serverManager.StopStreaming();
    }
}
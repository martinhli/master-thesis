using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using TMPro;
using Data; // for Ship, KLVFrameMetadata, AISData if needed

public class MetricsManager : MonoBehaviour
{
    public static MetricsManager Instance { get; private set; }

    [Header("General")]
    public bool metricsEnabled = true;
    public bool logToConsole = true;

    [Header("Click-based accuracy")]
    public float maxClickAssociationDistance = 100f;

    [Header("Accuracy conditions")]
    public string currentCondition = "Default";
    public TextMeshProUGUI conditionLabel;

    [Header("Condition Click Counters")]
    public int wideFovClicks = 0;
    public int zoomedInClicks = 0;
    public int fastPanClicks = 0;
    public TextMeshProUGUI clickCountLabel;

    [Header("CSV file logging")]
    [Tooltip("Folder name under Application.persistentDataPath where CSV files are stored.")]
    public string metricsFolderName = "MetricsLogs";
    private string _metricsFolderPath;

    private StreamWriter _latencyWriter;
    private StreamWriter _accuracyWriter;

    // Latency state
    private double lastMetadataUnityTime = -1.0;
    private long lastMetadataFrameId = 0;
    private long frameCounter = 0;

    // Overlay accuracy state
    private readonly List<Vector2> lastOverlayPoints = new();
    private readonly List<Ship> lastOverlayShips = new();
    private long lastOverlayFrameId = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!metricsEnabled)
            return;
        
        if (conditionLabel != null)
            conditionLabel.text = $"Condition: {currentCondition}";

        Debug.Log(Application.persistentDataPath);

        // Create a metrics folder under consistent file path
        _metricsFolderPath = Path.Combine(Application.persistentDataPath, metricsFolderName);
        if (!Directory.Exists(_metricsFolderPath))
            Directory.CreateDirectory(_metricsFolderPath);

        // Open latency CSV
        string latencyPath = Path.Combine(_metricsFolderPath, "latency.csv");
        _latencyWriter = new StreamWriter(latencyPath, false, Encoding.UTF8);
        _latencyWriter.WriteLine("frameId,unityTimeMs,latencyMs,shipCount");
        _latencyWriter.Flush();

        // Open accuracy CSV
        string accuracyPath = Path.Combine(_metricsFolderPath, "accuracy.csv");
        _accuracyWriter = new StreamWriter(accuracyPath, false, Encoding.UTF8);
        _accuracyWriter.WriteLine("frameId,unityTimeMs,shipName,xClick,yClick,xOverlay,yOverlay,pixelError,condition");
        _accuracyWriter.Flush();

        if (logToConsole) {
            Debug.Log($"[Metrics] Logging to folder: {_metricsFolderPath}");
        }
    }

    private void OnApplicationQuit()
    {
        CloseWriters();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            CloseWriters();
            Debug.Log(
            $"[Metrics] Click summary – " +
            $"WideFOV={wideFovClicks}, " +
            $"ZoomedIn={zoomedInClicks}, " +
            $"FastPan={fastPanClicks}");
    }

    private void CloseWriters()
    {
        if (_latencyWriter != null) {
            _latencyWriter.Flush();
            _latencyWriter.Close();
            _latencyWriter = null;
        }

        if (_accuracyWriter != null) {
            _accuracyWriter.Flush();
            _accuracyWriter.Close();
            _accuracyWriter = null;
        }
    }

    private void Update()
    {
        if (!metricsEnabled) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SetCondition("WideFOV");
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetCondition("ZoomedIn");
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetCondition("FastPan");

        // Left-click: accuracy test
        if (Input.GetMouseButtonDown(0))
        {
            HandleClickForAccuracy(Input.mousePosition);
        }
    }

    // --------------------------------------------------------------------
    // Public API – call these from your existing code
    // --------------------------------------------------------------------

    public void OnMetadataReceived(KLVFrameMetadata metadata)
    {
        if (!metricsEnabled || metadata == null)
            return;

        frameCounter++;
        lastMetadataFrameId = frameCounter;
        lastMetadataUnityTime = Time.realtimeSinceStartupAsDouble;

        Debug.Log($"[Metrics] Metadata received at {lastMetadataUnityTime:F3}, frame={lastMetadataFrameId}");
    }

    public void OnOverlaysRendered(List<Ship> ships, Vector2[] screenPoints)
    {
        if (!metricsEnabled || ships == null || screenPoints == null)
            return;

        lastOverlayPoints.Clear();
        lastOverlayShips.Clear();

        int count = Mathf.Min(ships.Count, screenPoints.Length);
        for (int i = 0; i < count; i++)
        {
            lastOverlayPoints.Add(screenPoints[i]);
            lastOverlayShips.Add(ships[i]);
        }
        lastOverlayFrameId = frameCounter;

        // Latency: metadata arrival -> overlay render
        double now = Time.realtimeSinceStartupAsDouble;

        if (lastMetadataUnityTime <= 0.0)
        {
            if (logToConsole)
                Debug.LogWarning("[Metrics] Overlays rendered but no metadata timestamp yet, skipping latency log.");
            return;
        }

        double latencyMs = (now - lastMetadataUnityTime) * 1000.0;
        double nowMs = now * 1000.0;

        if (logToConsole)
        {
            Debug.Log(
                $"[Metrics] Latency frame={lastOverlayFrameId} : {latencyMs:F2} ms " +
                $"(ships={count})");
        }

        if (_latencyWriter != null)
        {
            string line = $"{lastOverlayFrameId},{nowMs:F1},{latencyMs:F2},{count}";
            _latencyWriter.WriteLine(line);
            _latencyWriter.Flush();
        }
    }

    // --------------------------------------------------------------------
    // Accuracy – click-based error
    // --------------------------------------------------------------------

    private void HandleClickForAccuracy(Vector3 mousePos)
    {
        if (lastOverlayPoints == null || lastOverlayPoints.Count == 0)
        {
            if (logToConsole)
                Debug.LogWarning("Metrics: No overlay points available for accuracy test.");
            return;
        }

        Vector2 click = new Vector2(mousePos.x, mousePos.y);

        float bestDist = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < lastOverlayPoints.Count; i++)
        {
            float d = Vector2.Distance(click, lastOverlayPoints[i]);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        // Update per-condition counters
        switch (currentCondition)
        {
            case "WideFOV":
                wideFovClicks++;
                break;
            case "ZoomedIn":
                zoomedInClicks++;
                break;
            case "FastPan":
                fastPanClicks++;
                break;
        }

        // Optional: update on-screen UI
        if (clickCountLabel != null)
        {
            clickCountLabel.text =
                $"WideFOV: {wideFovClicks}  |  " +
                $"ZoomedIn: {zoomedInClicks}  |  " +
                $"FastPan: {fastPanClicks}";
        }

        if (bestIndex < 0 || bestDist > maxClickAssociationDistance)
        {
            if (logToConsole)
                Debug.LogWarning($"Metrics: Click ignored (no overlay within {maxClickAssociationDistance} px).");
            return;
        }

        float pixelError = bestDist;
        Ship ship = lastOverlayShips != null && bestIndex < lastOverlayShips.Count
            ? lastOverlayShips[bestIndex]
            : null;

        string shipName = ship != null ? ship.name : "Unknown";

        if (logToConsole)
        {
            Debug.Log(
                $"[Metrics] Accuracy frame={lastOverlayFrameId}, ship={shipName}, " +
                $"error={pixelError:F2} px");
        }

        if (_accuracyWriter != null)
        {
            double nowMs = Time.realtimeSinceStartupAsDouble * 1000.0;
            Vector2 overlay = lastOverlayPoints[bestIndex];

            _accuracyWriter.WriteLine(
                $"{lastOverlayFrameId},{nowMs:F1},{shipName}," +
                $"{click.x:F2},{click.y:F2}," +
                $"{overlay.x:F2},{overlay.y:F2}," +
                $"{pixelError:F2},{currentCondition}");
            _accuracyWriter.Flush();
        }
    }

    private void SetCondition(string newCondition)
    {
        if (currentCondition == newCondition)
            return;

        currentCondition = newCondition;

        if (conditionLabel != null)
            conditionLabel.text = $"Condition: {currentCondition}";

        Debug.Log($"[Metrics] Condition changed to: {currentCondition}");
    }
}

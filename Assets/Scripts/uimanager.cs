using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;
using TMPro;
using Data;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Diagnostics;
using System.Numerics;

using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

public class UIManager : MonoBehaviour
{
    public enum StudyScenario
    {
        AISDeterministicBaseline = 1,
        RadarEOIRDegraded = 2,
        FusedUncertaintyAware = 3
    }

    [Header("Panel References")]
    public GameObject taskInstructionPanel;
    public GameObject contactListPanel;
    public GameObject confirmationStatusPanel;
    public GameObject progressPanel;

    [Header("Task Instructions")]
    public TextMeshProUGUI taskTitleText;
    public TextMeshProUGUI taskInstructionText;

    [Header("Contact List")]
    public Transform contactListContent;
    public GameObject contactListItemPrefab;
    public float contactItemSpacing = 12f;
    public float contactItemPreferredHeight = 90f;
    public bool enforceContactItemLayoutElement = true;

    [Header("Confirmation Status")]
    public TextMeshProUGUI confirmationStatusText;
    public Image statusIcon;
    public TextMeshProUGUI detailText;
    public Color readyColor = Color.white;
    public Color captureColor = Color.cyan;
    public Color confirmedColor = Color.green;
    public Color rejectedColor = Color.red;

    [Header("Progress Status")]
    public TextMeshProUGUI contactsConfirmedText;
    public TextMeshProUGUI timerText;
    public Slider progressBar;

    [Header("Test Data")]
    public List<Track> currentContacts = new List<Track>();
    public int confirmedContacts = 0;
    public int totalContacts = 0;
    public float taskStartTime;
    public bool taskActive = false;

    [Header("Test Settings")]
    public TrackManager trackManager;
    public bool autoStartTaskWhenTracksAvailable = true;
    public float statusResetDelaySeconds = 2f;
    public float contactListRefreshIntervalSeconds = 0.5f;
    public float contactShipAssociationDistanceThreshold = 1200f;

    [Header("Scenario Settings")]
    public StudyScenario scenario = StudyScenario.RadarEOIRDegraded;
    public bool applyScenarioInstructionsOnStart = true;
    public StudyScenarioController scenarioController;
    public TMP_Dropdown scenarioDropdown;

    private Dictionary<string, GameObject> contactListItems = new Dictionary<string, GameObject>();
    private HashSet<string> confirmedTrackIds = new HashSet<string>();
    private HashSet<string> requiredTrackIds = new HashSet<string>();
    private Coroutine resetStatusCoroutine;
    private float nextContactRefreshTime;
    private bool taskCompletedLock = false;
    private List<SimulatedShip> aisBaselineShips = new List<SimulatedShip>();
    private int currentAisTargetIndex = 0;
    private float aisClickRaycastDistance = 15000f;
    private float aisClickSphereCastRadius = 100f;
    
    private InputDevice leftController;
    private bool leftControllerActive = false;
    private bool previousLeftGripState = false;
    private bool previousLeftPrimaryButtonState = false;
    private Canvas scenarioDropdownCanvas;
    private Canvas hudCanvas;
    private Camera eoirReferenceCamera;
    private ScenarioMetricsManager metricsCollector;

    public bool enableLeftGripScenarioSwitch = true;
    public float leftControllerGripThreshold = 0.55f;
    public bool enableLeftPrimaryParticipantAdvance = true;

    [Header("VR Dropdown Placement")]
    public bool lockScenarioDropdownToHeadset = true;
    public bool forceHudToMainCamera = true;
    public string hudCanvasObjectName = "HUD";
    public Vector3 scenarioDropdownLocalOffset = new Vector3(0f, -0.08f, 1.0f);
    public Vector3 scenarioDropdownLocalEuler = Vector3.zero;
    public Vector3 scenarioDropdownLocalScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

    [Header("Main Camera Alignment")]
    public bool alignMainCameraToEOIR = true;
    public string eoirCameraObjectName = "EOIRCamera";

    void Start()
    {
        if (scenarioController == null)
        {
            scenarioController = FindFirstObjectByType<StudyScenarioController>();
        }

        // Initialize UI by setting task instructions and setting contact status
        InitializeUI();
        InitializeScenarioSelectorUI();
        HookScenarioDropdown();
        EnsureScenarioDropdownVisibleInVR();
        AlignMainCameraToEOIRSettings();

        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }

        metricsCollector = FindFirstObjectByType<ScenarioMetricsManager>();
        if (metricsCollector == null)
        {
            metricsCollector = gameObject.AddComponent<ScenarioMetricsManager>();
        }

        ApplyContactListLayoutSettings();

        // If auto-start is enabled, check for existing tracks and start task if any are found
        TryAutoStartTask();

        
    }

    void OnEnable()
    {
        HookScenarioDropdown();
        EnsureScenarioDropdownVisibleInVR();
    }

    void Update()
    {
        HandleParticipantAdvanceInput();
        HandleScenarioSwitchInput();
        EnsureScenarioDropdownVisibleInVR();
        AlignMainCameraToEOIRSettings();

        RefreshContactList();

        // Scenario 1 should be operable even before sensor-track auto-start warms up.
        if (scenario == StudyScenario.AISDeterministicBaseline)
        {
            EnsureAISBaselineReady();
            HandleAISBaselineInput();
        }

        if (!taskActive)
        {
            TryAutoStartTask();
            return;
        }

        UpdateTimer();
        UpdateProgress();
    }

    void LateUpdate()
    {
    }

    void InitializeUI()
    {
        if (applyScenarioInstructionsOnStart)
        {
            ApplyScenarioInstructions();
        }
        else
        {
            SetConfirmationStatus("READY", "Waiting to start...", readyColor);
        }

        UpdateProgress();
        UpdateTimer();
    }

    public void ConfigureScenario(StudyScenario selectedScenario)
    {
        scenario = selectedScenario;
        ResetTaskStateForScenarioSwitch();

        if (scenario == StudyScenario.AISDeterministicBaseline)
        {
            // Reset baseline sequence when entering Scenario 1.
            currentAisTargetIndex = 0;
            aisBaselineShips.Clear();
        }
        else
        {
            currentAisTargetIndex = 0;
            aisBaselineShips.Clear();
        }

        ApplyScenarioInstructions();
        RefreshRequiredTargetsFromScene();
        UpdateProgress();
        UpdateTimer();
    }

    private void ResetTaskStateForScenarioSwitch()
    {
        taskCompletedLock = false;
        taskActive = false;
        taskStartTime = Time.time;

        confirmedContacts = 0;
        totalContacts = 0;
        confirmedTrackIds.Clear();
        requiredTrackIds.Clear();

        currentContacts.Clear();
        nextContactRefreshTime = 0f;

        CancelPendingStatusReset();
    }

    public bool IsTrackConfirmed(string trackId)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            return false;
        }

        return confirmedTrackIds.Contains(trackId);
    }

    public bool IsShipConfirmed(SimulatedShip ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(ship.mmsi) && confirmedTrackIds.Contains($"AIS_{ship.mmsi}"))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(ship.shipName) && confirmedTrackIds.Contains(ship.shipName))
        {
            return true;
        }

        return false;
    }

    public bool IsEOIRAvailableForCurrentScenario()
    {
        return scenario != StudyScenario.AISDeterministicBaseline;
    }

    private bool IsKnownRadarContact(Track track)
    {
        if (track == null)
        {
            return false;
        }

        if (IsTrackConfirmed(track.trackid))
        {
            return true;
        }

        SimulatedShip ship = FindBestShipForTrack(track);
        if (ship != null)
        {
            return ship.aisTransponder;
        }

        // If we cannot match to a scene ship, keep a conservative fallback.
        return track.shipData != null && !string.IsNullOrEmpty(track.shipData.name);
    }

    private SimulatedShip FindBestShipForTrack(Track track)
    {
        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        SimulatedShip bestShip = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < ships.Length; i++)
        {
            SimulatedShip ship = ships[i];
            if (ship == null)
            {
                continue;
            }

            float distance = Vector3.Distance(track.position, ship.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestShip = ship;
            }
        }

        if (bestShip != null && bestDistance <= Mathf.Max(0f, contactShipAssociationDistanceThreshold))
        {
            return bestShip;
        }

        return null;
    }

    private void ApplyScenarioInstructions()
    {
        switch (scenario)
        {
            case StudyScenario.AISDeterministicBaseline:
                string aisInstructions = "Use the AIS labels on the ships to locate the ship with the matching MMSI as CURRENT SHIP AIS MMSI." +
                "Use the sensor contact list and look around the scene to find and confirm each ship." +
                "Press the [LEFT TRIGGER] while aiming the left hand controller at a ship to confirm its identity.";
                if (currentAisTargetIndex < aisBaselineShips.Count)
                {
                    SimulatedShip targetShip = aisBaselineShips[currentAisTargetIndex];
                    if (targetShip != null && !string.IsNullOrEmpty(targetShip.mmsi))
                    {
                        aisInstructions += $"\n\nCURRENT SHIP AIS MMSI: {targetShip.mmsi}";
                    }
                }
                SetTaskInstructions(
                    "SCENARIO 1: AIS DETERMINISTIC BASELINE",
                    aisInstructions);
                SetConfirmationStatus("READY", "Awaiting target confirmation...", readyColor);
                break;

            case StudyScenario.RadarEOIRDegraded:
                SetTaskInstructions(
                    "SCENARIO 2: RADAR + EO/IR DEGRADED",
                    "Find unknown contacts by looking in the scene and in the sensor contact list." +
                    "Use the EO/IR camera to identify the unknown contact." +
                    "Move the EO/IR camera with [RIGHT THUMBSTICK], capture with [A] and reset view with [B]." + 
                    "Use the [RIGHT TRIGGER] to zoom in and [RIGHT GRIP] to zoom out.");
                SetConfirmationStatus("READY", "Awaiting target confirmation...", readyColor);
                break;

            case StudyScenario.FusedUncertaintyAware:
                SetTaskInstructions(
                    "SCENARIO 3: FUSED UNCERTAINTY-AWARE",
                    "Use the fused AIS/radar labels and uncertainty cues to confirm the identity of the most uncertain ships." +
                    "Use the EO/IR camera to identify the ships with uncertain identities." +
                    "Move the EO/IR camera with [RIGHT THUMBSTICK], capture with [A] and reset view with [B]." + 
                    "Use the [RIGHT TRIGGER] to zoom in and [RIGHT GRIP] to zoom out.");
                SetConfirmationStatus("READY", "Awaiting target confirmation...", readyColor);
                break;
        }
    }

    private void InitializeScenarioSelectorUI()
    {
        if (scenarioDropdown == null)
        {
            return;
        }

        if (scenarioDropdown.options == null || scenarioDropdown.options.Count == 0)
        {
            scenarioDropdown.ClearOptions();
            scenarioDropdown.AddOptions(new List<string>
            {
                "Scenario 1 - AIS Baseline",
                "Scenario 2 - Radar + EO/IR",
                "Scenario 3 - Fused + Uncertainty"
            });
        }

        scenarioDropdown.SetValueWithoutNotify(Mathf.Clamp(((int)scenario) - 1, 0, 2));
    }

    private void HookScenarioDropdown()
    {
        if (scenarioDropdown == null)
        {
            scenarioDropdown = FindFirstObjectByType<TMP_Dropdown>();
        }

        if (scenarioDropdown == null)
        {
            return;
        }

        scenarioDropdown.onValueChanged.RemoveListener(OnScenarioDropdownChanged);
        scenarioDropdown.onValueChanged.AddListener(OnScenarioDropdownChanged);

        EnsureScenarioDropdownVisibleInVR();
    }

    private void EnsureScenarioDropdownVisibleInVR()
    {
        if (scenarioDropdownCanvas == null)
        {
            if (scenarioDropdown != null)
            {
                scenarioDropdownCanvas = scenarioDropdown.GetComponentInParent<Canvas>();
            }

            // Fallback for cases where the dropdown reference is missing in Inspector.
            if (scenarioDropdownCanvas == null)
            {
                if (hudCanvas == null)
                {
                    GameObject hudObject = GameObject.Find(hudCanvasObjectName);
                    if (hudObject != null)
                    {
                        hudCanvas = hudObject.GetComponent<Canvas>();
                    }
                }

                scenarioDropdownCanvas = hudCanvas;
            }
        }

        if (scenarioDropdownCanvas == null)
        {
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return;
        }

        scenarioDropdownCanvas.enabled = true;
        scenarioDropdownCanvas.overrideSorting = true;
        scenarioDropdownCanvas.sortingOrder = 5000;
        scenarioDropdownCanvas.worldCamera = mainCam;
        scenarioDropdownCanvas.planeDistance = Mathf.Max(0.4f, scenarioDropdownCanvas.planeDistance);

        if (!lockScenarioDropdownToHeadset && !forceHudToMainCamera)
        {
            if (scenarioDropdownCanvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                scenarioDropdownCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            }
            return;
        }

        if (scenarioDropdownCanvas.renderMode != RenderMode.WorldSpace)
        {
            scenarioDropdownCanvas.renderMode = RenderMode.WorldSpace;
        }

        Transform canvasTransform = scenarioDropdownCanvas.transform;
        if (canvasTransform.parent != mainCam.transform)
        {
            canvasTransform.SetParent(mainCam.transform, false);
        }

        RectTransform rect = scenarioDropdownCanvas.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localPosition = scenarioDropdownLocalOffset;
            rect.localRotation = Quaternion.Euler(scenarioDropdownLocalEuler);
            rect.localScale = scenarioDropdownLocalScale;
        }
    }

    private Camera TryResolveEOIRCamera()
    {
        if (eoirReferenceCamera != null)
        {
            return eoirReferenceCamera;
        }

        GameObject eoirObject = GameObject.Find(eoirCameraObjectName);
        if (eoirObject != null)
        {
            eoirReferenceCamera = eoirObject.GetComponent<Camera>();
        }

        return eoirReferenceCamera;
    }

    private void AlignMainCameraToEOIRSettings()
    {
        if (!alignMainCameraToEOIR)
        {
            return;
        }

        Camera mainCam = Camera.main;
        Camera eoirCam = TryResolveEOIRCamera();
        if (mainCam == null || eoirCam == null)
        {
            return;
        }

        // Copy core camera state so both views use the same render configuration.
        mainCam.clearFlags = eoirCam.clearFlags;
        mainCam.backgroundColor = eoirCam.backgroundColor;
        mainCam.allowHDR = eoirCam.allowHDR;
        mainCam.allowMSAA = eoirCam.allowMSAA;
        mainCam.useOcclusionCulling = eoirCam.useOcclusionCulling;
        mainCam.cullingMask = eoirCam.cullingMask;

        mainCam.nearClipPlane = Mathf.Max(0.03f, eoirCam.nearClipPlane);
        mainCam.farClipPlane = eoirCam.farClipPlane;
        mainCam.fieldOfView = eoirCam.fieldOfView;

        UniversalAdditionalCameraData mainData = mainCam.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData eoirData = eoirCam.GetUniversalAdditionalCameraData();
        if (mainData != null && eoirData != null)
        {
            mainData.renderShadows = eoirData.renderShadows;
            mainData.antialiasing = eoirData.antialiasing;
            mainData.antialiasingQuality = eoirData.antialiasingQuality;
            mainData.stopNaN = eoirData.stopNaN;
            mainData.dithering = eoirData.dithering;
            mainData.renderPostProcessing = eoirData.renderPostProcessing;
            mainData.volumeLayerMask = eoirData.volumeLayerMask;
            mainData.volumeTrigger = eoirData.volumeTrigger;
            mainData.requiresColorOption = eoirData.requiresColorOption;
            mainData.requiresDepthOption = eoirData.requiresDepthOption;
        }
    }

    public void OnScenarioDropdownChanged(int index)
    {
        StudyScenario selectedScenario = (StudyScenario)Mathf.Clamp(index + 1, 1, 3);

        if (scenarioController != null)
        {
            scenarioController.ApplyScenario(selectedScenario);
            return;
        }

        ConfigureScenario(selectedScenario);
    }

    private void TryAutoStartTask()
    {
        if (taskActive || taskCompletedLock || !autoStartTaskWhenTracksAvailable)
        {
            return;
        }

        if (trackManager == null)
        {
            return;
        }

        List<Track> scenarioContacts = GetScenarioContactsFromTrackManager();
        if (scenarioContacts != null && scenarioContacts.Count > 0)
        {
            StartTask(scenarioContacts);
            return;
        }

        // Fallback to all active tracks so task/list still starts even if sensor-tag filtering has not stabilized yet.
        List<Track> activeTracks = trackManager.GetActiveTracks();
        if (activeTracks != null && activeTracks.Count > 0)
        {
            StartTask(activeTracks);
        }
    }

    private void EnsureTaskStarted()
    {
        if (taskCompletedLock)
        {
            return;
        }

        if (taskActive)
        {
            return;
        }

        TryAutoStartTask();

        if (taskActive)
        {
            return;
        }

        taskActive = true;
        taskStartTime = Time.time;
        SetConfirmationStatus("READY", "Task started.", readyColor);
    }

    public void StartTask(List<Track> contacts)
    {
        taskCompletedLock = false;
        taskActive = true;
        taskStartTime = Time.time;
        confirmedContacts = 0;
        confirmedTrackIds.Clear();
        currentContacts = contacts;
        totalContacts = currentContacts.Count;
        RefreshRequiredTargetsFromScene();

        if (metricsCollector != null)
        {
            metricsCollector.BeginTask(scenario.ToString());
            RegisterTrackAppearances(currentContacts);
        }
        
        if (scenario == StudyScenario.AISDeterministicBaseline)
        {
            InitializeAISBaselineShips();
        }
        
        nextContactRefreshTime = 0f;

        PopulateContactList();
        UpdateProgress();
        UpdateTimer();

        Debug.Log($"[UIManager] Task started with {totalContacts} contacts.");
    }

    private void RegisterTrackAppearances(List<Track> contacts)
    {
        if (metricsCollector == null || contacts == null)
        {
            return;
        }

        for (int i = 0; i < contacts.Count; i++)
        {
            Track track = contacts[i];
            if (track == null || string.IsNullOrEmpty(track.trackid))
            {
                continue;
            }

            metricsCollector.RegisterTargetAppearance(track.trackid);
        }
    }

    public void SetTaskInstructions(string title, string instructions)
    {
        if (taskTitleText != null)
        {
            taskTitleText.text = title;
        }

        if (taskInstructionText != null)
        {
            taskInstructionText.text = instructions;
        }
    }

    public void SetConfirmationStatus(string status, string details, Color color)
    {
        if (confirmationStatusText != null)
        {
            confirmationStatusText.text = status;
            confirmationStatusText.color = color;
        }

        if (statusIcon != null)
        {
            statusIcon.color = color;
        }

        if (detailText != null)
        {
            detailText.text = details;
        }
        Debug.Log($"UIManager.SetConfirmationStatus: status={status} color={color}");
    }

    

    public void PopulateContactList()
    {
        ApplyContactListLayoutSettings();

        // Clear existing contact list items
        foreach (var item in contactListItems.Values)
        {
            Destroy(item);
        }
        contactListItems.Clear();

        // Show newest contacts first so operators can quickly map fresh labels to list entries.
        List<Track> orderedContacts = GetContactsSortedNewestFirst(currentContacts);
        foreach (Track track in orderedContacts)
        {
            CreateContactListItem(track);
        }
    }

    private List<Track> GetContactsSortedNewestFirst(List<Track> contacts)
    {
        List<Track> result = new List<Track>();
        if (contacts == null || contacts.Count == 0)
        {
            return result;
        }

        for (int i = 0; i < contacts.Count; i++)
        {
            if (contacts[i] != null)
            {
                result.Add(contacts[i]);
            }
        }

        result.Sort((a, b) =>
        {
            int byStatus = GetContactSortPriority(a).CompareTo(GetContactSortPriority(b));
            if (byStatus != 0)
            {
                return byStatus;
            }

            int byTime = b.timeStamp.CompareTo(a.timeStamp);
            if (byTime != 0)
            {
                return byTime;
            }

            string aId = a.trackid ?? string.Empty;
            string bId = b.trackid ?? string.Empty;
            return string.CompareOrdinal(aId, bId);
        });

        return result;
    }

    public void CreateContactListItem(Track track)
    {
        if (contactListItemPrefab != null && contactListContent != null)
        {
            GameObject item = Instantiate(contactListItemPrefab, contactListContent);
            item.name = $"Contact_{track.trackid}";

            if (enforceContactItemLayoutElement)
            {
                LayoutElement element = item.GetComponent<LayoutElement>();
                if (element == null)
                {
                    element = item.AddComponent<LayoutElement>();
                }

                float rowHeight = Mathf.Max(20f, contactItemPreferredHeight);
                element.minHeight = rowHeight;
                element.preferredHeight = rowHeight;
                element.flexibleHeight = 0f;
            }

            // Set contact details in the UI elements of the item
            TextMeshProUGUI idText = item.transform.Find("ContactIDText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI distText = item.transform.Find("DistanceText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI bearingText = item.transform.Find("BearingText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI statusText = item.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            Image statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();

            bool isKnown = IsKnownRadarContact(track);

            if (idText != null) idText.text = GetRadarId(track);
            if (distText != null)
            {
                float distance = Vector3.Distance(Vector3.zero, track.position) / 1000f;;
                distText.text = $"{distance:F1} km"; 
            } 
            if (bearingText != null)
            {
                float bearing = Mathf.Atan2(track.position.x, track.position.z) / (Mathf.PI / 180f);
                if (bearing < 0) bearing += 360f;
                bearingText.text = $"{bearing:F0}°";
            }
            if (statusText != null)
            {
                if (scenario == StudyScenario.FusedUncertaintyAware)
                {
                    statusText.text = "TRACKED";
                    statusText.color = Color.white;
                }
                else
                {
                    statusText.text = isKnown ? "KNOWN" : "UNKNOWN";
                    statusText.color = isKnown ? Color.green : Color.yellow;
                }
            }
            // Default to yellow for unknown status, will be updated to green/red if user confirms/rejects.
            if (statusIcon != null) statusIcon.color = isKnown ? Color.green : Color.yellow;

            contactListItems[track.trackid] = item;
        }
    }

    private string GetRadarId(Track track)
    {
        if (track != null && !string.IsNullOrEmpty(track.trackid))
        {
            return track.trackid;
        }

        return "RADAR_UNKNOWN";
    }

    private int GetContactSortPriority(Track track)
    {
        if (track == null)
        {
            return 3;
        }

        bool isConfirmed = IsTrackConfirmed(track.trackid);
        bool isUnknown = !IsKnownRadarContact(track);

        if (!isConfirmed && isUnknown)
        {
            return 0;
        }

        if (!isConfirmed && !isUnknown)
        {
            return 1;
        }

        return 2;
    }

    private void ApplyContactListLayoutSettings()
    {
        if (contactListContent == null)
        {
            return;
        }

        VerticalLayoutGroup layout = contactListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        layout.spacing = Mathf.Max(0f, contactItemSpacing);
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
    }

    public void UpdateContactStatus(string trackID, bool confirmed)
    {
        if (string.IsNullOrEmpty(trackID))
        {
            return;
        }

        if (contactListItems.ContainsKey(trackID))
        {
            // Update the status icon color for the contact list item
            GameObject item = contactListItems[trackID];
            TextMeshProUGUI statusText = item.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            Image statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();

            if (statusIcon != null)
            {
                statusIcon.color = confirmed ? Color.green : Color.red;
            }

            if (statusText != null)
            {
                // In the fused scenario, we want to keep the "TRACKED" label but just update the color to reflect confirmation status,
                if (scenario == StudyScenario.FusedUncertaintyAware)
                {
                    statusText.text = "TRACKED";
                    statusText.color = Color.white;
                }
                else
                {
                    statusText.text = confirmed ? "KNOWN" : "UNKNOWN";
                    statusText.color = confirmed ? Color.green : Color.yellow;
                }
            }
        }

        if (confirmed)
        {
            // Count each track only once, even if user captures it multiple times.
            if (confirmedTrackIds.Add(trackID))
            {
                confirmedContacts = confirmedTrackIds.Count;
            }

            // Rebuild once so the newly confirmed contact moves out of the unknown-first section.
            PopulateContactList();
        }


        Debug.Log($"[UIManager] Contact {trackID} status: {(confirmed ? "CONFIRMED" : "REJECTED")}");

        UpdateProgress();
    }

    // Need two functions that can update the progress panel with current confirmed contacts and elapsed time in minutes and seconds format
    public void UpdateTimer()
    {
        if (timerText == null) return;
        float elapsedTime = taskActive ? (Time.time - taskStartTime) : 0f;
        if (elapsedTime < 0f)
        {
            elapsedTime = 0f;
        }
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";    
    }

    public void UpdateProgress()
    {
        int confirmedTargetCount = GetConfirmedRequiredTargetCount();
        int requiredTargetCount = requiredTrackIds.Count;
        bool useTargetCompletion = ShouldUseTargetCompletion();

        if (contactsConfirmedText != null)
        {
            contactsConfirmedText.text = useTargetCompletion
                ? $"Targets Confirmed: {confirmedTargetCount} / {requiredTargetCount}"
                : $"Confirmed: {confirmedContacts} / {totalContacts}";
        }
        
        if (progressBar != null)
        {
            if (useTargetCompletion && requiredTargetCount > 0)
            {
                progressBar.value = (float)confirmedTargetCount / requiredTargetCount;
            }
            else if (totalContacts > 0)
            {
                progressBar.value = (float)confirmedContacts / totalContacts;
            }
        }

        // Check if task is complete and update status panel accordingly
        if (taskActive)
        {
            if (useTargetCompletion)
            {
                if (requiredTargetCount > 0 && confirmedTargetCount >= requiredTargetCount)
                {
                    CompleteTask();
                }
            }
            else if (totalContacts > 0 && confirmedContacts >= totalContacts)
            {
                CompleteTask();
            }
        }
    }

    public void CompleteTask()
    {
        // Set task as inactive, record completion time, and update status panel to show completion
        taskActive = false;
        taskCompletedLock = true;

        float completionTime = Time.time - taskStartTime;
        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);

        SetConfirmationStatus("TASK COMPLETE",
        ShouldUseTargetCompletion()
            ? $"All unknown target contacts confirmed in {minutes:00}:{seconds:00}"
            : $"All contacts confirmed in {minutes:00}:{seconds:00}",
        confirmedColor);

        if (metricsCollector != null)
        {
            metricsCollector.EndTask(true);
        }

        Debug.Log($"[UIManager] Task complete in {minutes:00}:{seconds:00}. Contacts confirmed: {confirmedContacts}/{totalContacts}");
    }

    // Public methods for external scripts to call when confirming or rejecting contacts
    public void OnCaptureStarted()
    {
        if (!IsEOIRAvailableForCurrentScenario())
        {
            SetConfirmationStatus("INFO", "EO/IR tasking is disabled in Scenario 1.", readyColor);
            return;
        }

        EnsureTaskStarted();
        CancelPendingStatusReset();
        SetConfirmationStatus("CAPTURING", "EO/IR camera is capturing...", captureColor);
    }

    public void OnAnalyzing()
    {
        CancelPendingStatusReset();
        SetConfirmationStatus("ANALYZING", "Running detection...", captureColor);
    }

    public void OnShipConfirmed(string trackID)
    {
        EnsureTaskStarted();
        SetConfirmationStatus("CONFIRMED", "Visual contact verified", confirmedColor);
        if (metricsCollector != null)
        {
            metricsCollector.RegisterCorrectConfirmation(trackID);
        }
        UpdateContactStatus(trackID, true);
        ScheduleStatusReset();
    }

    public void OnNoShipDetected(string trackID)
    {
        EnsureTaskStarted();
        SetConfirmationStatus("REJECTED", "False alarm or missed detection", rejectedColor);
        if (metricsCollector != null)
        {
            metricsCollector.RegisterWrongConfirmation(trackID, "wrong_confirmation");
        }
        UpdateContactStatus(trackID, false);
        ScheduleStatusReset();
    }

    public void TogglePanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
    }
    public void ResetStatus()
    {
        SetConfirmationStatus("READY", "Waiting for contact...", readyColor);
    }

    private void CancelPendingStatusReset()
    {
        if (resetStatusCoroutine != null)
        {
            StopCoroutine(resetStatusCoroutine);
            resetStatusCoroutine = null;
        }
    }

    private void ScheduleStatusReset()
    {
        CancelPendingStatusReset();

        if (statusResetDelaySeconds <= 0f)
        {
            ResetStatus();
            return;
        }

        resetStatusCoroutine = StartCoroutine(ResetStatusAfterDelay());
    }

    private System.Collections.IEnumerator ResetStatusAfterDelay()
    {
        yield return new WaitForSecondsRealtime(statusResetDelaySeconds);
        resetStatusCoroutine = null;
        ResetStatus();
    }

    private void RefreshRequiredTargetsFromScene()
    {
        requiredTrackIds.Clear();

        if (scenario == StudyScenario.AISDeterministicBaseline)
        {
            // For AIS baseline, all ships with AIS are required targets
            SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
            foreach (SimulatedShip ship in ships)
            {
                if (ship == null || !ship.aisTransponder)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(ship.mmsi))
                {
                    requiredTrackIds.Add($"AIS_{ship.mmsi}");
                }
                else if (!string.IsNullOrEmpty(ship.shipName))
                {
                    requiredTrackIds.Add(ship.shipName);
                }
            }
            return;
        }

        SimulatedShip[] allShips = FindObjectsOfType<SimulatedShip>();
        foreach (SimulatedShip ship in allShips)
        {
            if (ship == null || ship.aisTransponder)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(ship.mmsi))
            {
                requiredTrackIds.Add($"AIS_{ship.mmsi}");
            }
            else if (!string.IsNullOrEmpty(ship.shipName))
            {
                requiredTrackIds.Add(ship.shipName);
            }
        }
    }

    private int GetConfirmedRequiredTargetCount()
    {
        int count = 0;
        foreach (string requiredId in requiredTrackIds)
        {
            if (confirmedTrackIds.Contains(requiredId))
            {
                count++;
            }
        }

        return count;
    }

    private bool ShouldUseTargetCompletion()
    {
        return scenario != StudyScenario.AISDeterministicBaseline && requiredTrackIds.Count > 0;
    }

    private void RefreshContactList()
    {
        if (trackManager == null)
        {
            return;
        }

        if (Time.time < nextContactRefreshTime)
        {
            return;
        }

        nextContactRefreshTime = Time.time + Mathf.Max(0.1f, contactListRefreshIntervalSeconds);

        List<Track> latestContacts = GetScenarioContactsFromTrackManager();
        currentContacts = latestContacts;
        totalContacts = currentContacts.Count;
        RegisterTrackAppearances(currentContacts);
        PopulateContactList();
    }

    private List<Track> GetScenarioContactsFromTrackManager()
    {
        if (trackManager == null)
        {
            return new List<Track>();
        }

        switch (scenario)
        {
            case StudyScenario.AISDeterministicBaseline:
                // AIS-only mode shows only tracks with AIS data, which are the ones the user can confirm in this scenario.
                return trackManager.GetTracksBySensorType(SensorType.AIS);

            case StudyScenario.RadarEOIRDegraded:
            {
                // Radar+EOIR mode shows only radar tracks, which are the ones the user can confirm with EO/IR in this scenario. 
                List<Track> radarTracks = trackManager.GetTracksBySensorType(SensorType.Radar);
                if (radarTracks != null && radarTracks.Count > 0)
                {
                    return radarTracks;
                }

                // Fallback: show active tracks while radar-only set is still warming up.
                return trackManager.GetActiveTracks();
            }

            case StudyScenario.FusedUncertaintyAware:
                // Fused mode shows the complete current track picture.
                return trackManager.GetActiveTracks();

            default:
                return trackManager.GetActiveTracks();
        }
    }

    private void InitializeAISBaselineShips()
    {
        aisBaselineShips.Clear();
        currentAisTargetIndex = 0;

        SimulatedShip[] allShips = FindObjectsOfType<SimulatedShip>();
        foreach (SimulatedShip ship in allShips)
        {
            if (ship != null && ship.aisTransponder && !string.IsNullOrEmpty(ship.mmsi))
            {
                aisBaselineShips.Add(ship);
            }
        }

        Debug.Log($"[UIManager] AIS Baseline: Initialized {aisBaselineShips.Count} targets to locate.");
        ApplyScenarioInstructions();
    }

    private void HandleAISBaselineInput()
    {
        if (aisBaselineShips.Count == 0 || currentAisTargetIndex >= aisBaselineShips.Count)
        {
            return;
        }

        bool confirmationPressed = false;
        
        // Check for mouse input
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Ignore clicks on UI elements so dropdown/panels do not trigger ship-selection logic.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            confirmationPressed = true;
        }
        
        if (confirmationPressed)
        {
            SimulatedShip clickedShip = RaycastDetectShip();
            TryConfirmAisTargetFromSelection(clickedShip);
        }
    }

    public bool TryConfirmAisTargetFromSelection(SimulatedShip clickedShip)
    {
        if (scenario != StudyScenario.AISDeterministicBaseline)
        {
            return false;
        }

        if (aisBaselineShips.Count == 0 || currentAisTargetIndex >= aisBaselineShips.Count)
        {
            return false;
        }

        SimulatedShip targetShip = aisBaselineShips[currentAisTargetIndex];
        if (clickedShip == null)
        {
            SetConfirmationStatus("REJECTED", $"No vessel selected. Looking for MMSI {targetShip.mmsi}", rejectedColor);
            ScheduleStatusReset();
            return false;
        }

        if (clickedShip == targetShip)
        {
            string trackId = string.IsNullOrEmpty(clickedShip.mmsi) ? clickedShip.shipName : $"AIS_{clickedShip.mmsi}";
            OnShipConfirmed(trackId);
            AdvanceToNextAISTarget();
            return true;
        }

        if (metricsCollector != null)
        {
            string selectedTrackId = string.IsNullOrEmpty(clickedShip.mmsi) ? clickedShip.shipName : $"AIS_{clickedShip.mmsi}";
            string expectedTrackId = string.IsNullOrEmpty(targetShip.mmsi) ? targetShip.shipName : $"AIS_{targetShip.mmsi}";
            metricsCollector.RegisterWrongSelection(selectedTrackId, expectedTrackId);
        }

        SetConfirmationStatus("REJECTED", $"Wrong target. Looking for MMSI {targetShip.mmsi}, not {clickedShip.mmsi}", rejectedColor);
        ScheduleStatusReset();
        return false;
    }

    private SimulatedShip RaycastDetectShip()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return null;
        }

        // For VR, use camera center. For desktop, use mouse position.
        Vector2 raycastSource = Input.mousePresent ? Input.mousePosition : mainCam.pixelRect.center;
        Ray ray = mainCam.ScreenPointToRay(raycastSource);
        RaycastHit[] hits = Physics.SphereCastAll(ray, aisClickSphereCastRadius, aisClickRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        SimulatedShip bestShip = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            SimulatedShip ship = hits[i].collider.GetComponent<SimulatedShip>();
            if (ship == null)
            {
                ship = hits[i].collider.GetComponentInParent<SimulatedShip>();
            }

            if (ship != null && hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestShip = ship;
            }
        }

        return bestShip;
    }

    private void HandleScenarioSwitchInput()
    {
        if (!enableLeftGripScenarioSwitch)
        {
            return;
        }

        if (!TryIsLeftControllerGripPressed())
        {
            return;
        }

        int nextScenarioIndex = (((int)scenario - 1) + 1) % 3;
        if (scenarioDropdown != null)
        {
            scenarioDropdown.SetValueWithoutNotify(nextScenarioIndex);
        }

        OnScenarioDropdownChanged(nextScenarioIndex);
    }

    private void HandleParticipantAdvanceInput()
    {
        if (!enableLeftPrimaryParticipantAdvance)
        {
            return;
        }

        if (!TryIsLeftControllerPrimaryButtonPressed())
        {
            return;
        }

        if (metricsCollector == null)
        {
            metricsCollector = FindFirstObjectByType<ScenarioMetricsManager>();
        }

        if (metricsCollector == null)
        {
            return;
        }

        metricsCollector.AdvanceParticipantId();
        SetConfirmationStatus("INFO", $"Participant advanced to {metricsCollector.participantId}", captureColor);
        ScheduleStatusReset();
    }

    private void AdvanceToNextAISTarget()
    {
        currentAisTargetIndex++;

        if (currentAisTargetIndex >= aisBaselineShips.Count)
        {
            taskCompletedLock = true;
            SetConfirmationStatus("TASK COMPLETE", "All AIS targets have been successfully located and confirmed!", confirmedColor);
            Debug.Log("[UIManager] AIS Baseline task completed: all vessels confirmed.");
            return;
        }

        ResetStatus();
        ApplyScenarioInstructions();
        Debug.Log($"[UIManager] Advancing to target {currentAisTargetIndex + 1} of {aisBaselineShips.Count}");
    }
    
    private bool TryInitializeLeftController()
    {
        if (leftController.isValid)
        {
            return true;
        }

        // Preferred lookup for hand-specific devices.
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftController.isValid)
        {
            leftControllerActive = true;
            previousLeftGripState = false;
            previousLeftPrimaryButtonState = false;
            return true;
        }

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left,
            devices);

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
                devices);
        }

        if (devices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left, devices);
        }

        if (devices.Count == 0)
        {
            leftControllerActive = false;
            return false;
        }

        leftController = devices[0];
        leftControllerActive = true;
        previousLeftGripState = false;
        previousLeftPrimaryButtonState = false;
        return leftController.isValid;
    }
    
    private bool TryIsLeftControllerGripPressed()
    {
        if (!TryInitializeLeftController())
        {
            previousLeftGripState = false;
            return false;
        }

        bool gripButtonPressed;
        if (leftController.TryGetFeatureValue(CommonUsages.gripButton, out gripButtonPressed))
        {
            bool pressedThisFrame = gripButtonPressed && !previousLeftGripState;
            previousLeftGripState = gripButtonPressed;
            return pressedThisFrame;
        }

        float gripValue;
        if (!leftController.TryGetFeatureValue(CommonUsages.grip, out gripValue))
        {
            previousLeftGripState = false;
            return false;
        }

        bool currentGripState = gripValue > Mathf.Clamp01(leftControllerGripThreshold);
        bool pressedThisFrameFallback = currentGripState && !previousLeftGripState;
        previousLeftGripState = currentGripState;
        return pressedThisFrameFallback;
    }

    private bool TryIsLeftControllerPrimaryButtonPressed()
    {
        if (!TryInitializeLeftController())
        {
            previousLeftPrimaryButtonState = false;
            return false;
        }

        bool primaryButtonPressed;
        if (!leftController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonPressed))
        {
            previousLeftPrimaryButtonState = false;
            return false;
        }

        bool pressedThisFrame = primaryButtonPressed && !previousLeftPrimaryButtonState;
        previousLeftPrimaryButtonState = primaryButtonPressed;
        return pressedThisFrame;
    }

    private void EnsureAISBaselineReady()
    {
        if (scenario != StudyScenario.AISDeterministicBaseline || taskCompletedLock)
        {
            return;
        }

        if (aisBaselineShips.Count == 0)
        {
            InitializeAISBaselineShips();
            RefreshRequiredTargetsFromScene();
        }

        if (!taskActive && aisBaselineShips.Count > 0)
        {
            taskActive = true;
            taskStartTime = Time.time;
            UpdateProgress();
            UpdateTimer();
        }
    }

}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Diagnostics;
using System.Numerics;

using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

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
    public Color readyColor = Color.yellow;
    public Color captureColor = Color.cyan;
    public Color confirmedColor = Color.green;
    public Color rejectedColor = Color.red;

    [Header("Progress Status")]
    public TextMeshProUGUI contactsConfirmedText;
    public TextMeshProUGUI timerText;
    public Slider progressBar;

    [Header("Test Data")]
    public List<Track> radarContacts = new List<Track>();
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
    private HashSet<string> requiredTargetTrackIds = new HashSet<string>();
    private Coroutine resetStatusCoroutine;
    private float nextContactRefreshTime;
    private bool taskCompletedLock;

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

        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }

        ApplyContactListLayoutSettings();

        // If auto-start is enabled, check for existing tracks and start task if any are found
        TryAutoStartTask();
    }

    void Update()
    {
        RefreshContactList();

        if (!taskActive)
        {
            TryAutoStartTask();
            return;
        }

        UpdateTimer();
        UpdateProgress();
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
        taskCompletedLock = false;
        ApplyScenarioInstructions();
        RefreshRequiredTargetsFromScene();
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
                SetTaskInstructions(
                    "SCENARIO 1: AIS DETERMINISTIC BASELINE",
                    "Use AIS AR overlays to locate the target and confirm identity. No uncertainty cues or EO/IR support are available.");
                SetConfirmationStatus("READY", "Awaiting AIS-based target confirmation...", readyColor);
                break;

            case StudyScenario.RadarEOIRDegraded:
                SetTaskInstructions(
                    "SCENARIO 2: RADAR + EO/IR DEGRADED",
                    "Locate radar contacts, task EO/IR camera, and confirm the correct target manually.");
                SetConfirmationStatus("READY", "Awaiting radar contact confirmation with EO/IR...", readyColor);
                break;

            case StudyScenario.FusedUncertaintyAware:
                SetTaskInstructions(
                    "SCENARIO 3: FUSED UNCERTAINTY-AWARE",
                    "Use fused AIS/radar overlays and uncertainty cues to assess reliability. Task EO/IR when needed before confirming.");
                SetConfirmationStatus("READY", "Awaiting uncertainty-aware target confirmation...", readyColor);
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
            return;
        }

        scenarioDropdown.onValueChanged.RemoveListener(OnScenarioDropdownChanged);
        scenarioDropdown.onValueChanged.AddListener(OnScenarioDropdownChanged);
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
        radarContacts = contacts;
        totalContacts = radarContacts.Count;
        RefreshRequiredTargetsFromScene();
        nextContactRefreshTime = 0f;

        // Need a function to populate contact list panel with current contacts
        PopulateContactList();
        UpdateProgress();
        UpdateTimer();

        Debug.Log($"[UIManager] Task started with {totalContacts} contacts.");
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
        List<Track> orderedContacts = GetContactsSortedNewestFirst(radarContacts);
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
                statusText.text = isKnown ? "KNOWN" : "UNKNOWN";
                statusText.color = isKnown ? Color.green : Color.yellow;
            }
            if (statusIcon != null) statusIcon.color = Color.yellow;

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
                statusText.text = confirmed ? "KNOWN" : "UNKNOWN";
                statusText.color = confirmed ? Color.green : Color.yellow;
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
        float elapsedTime = Time.time - taskStartTime;
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";    
    }

    public void UpdateProgress()
    {
        int confirmedTargetCount = GetConfirmedRequiredTargetCount();
        int requiredTargetCount = requiredTargetTrackIds.Count;
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
        UpdateContactStatus(trackID, true);
        ScheduleStatusReset();
    }

    public void OnNoShipDetected(string trackID)
    {
        EnsureTaskStarted();
        SetConfirmationStatus("REJECTED", "False alarm or missed detection", rejectedColor);
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
        requiredTargetTrackIds.Clear();

        if (scenario == StudyScenario.AISDeterministicBaseline)
        {
            return;
        }

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        foreach (SimulatedShip ship in ships)
        {
            if (ship == null || ship.aisTransponder)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(ship.mmsi))
            {
                requiredTargetTrackIds.Add($"AIS_{ship.mmsi}");
            }
            else if (!string.IsNullOrEmpty(ship.shipName))
            {
                requiredTargetTrackIds.Add(ship.shipName);
            }
        }
    }

    private int GetConfirmedRequiredTargetCount()
    {
        int count = 0;
        foreach (string requiredId in requiredTargetTrackIds)
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
        return scenario != StudyScenario.AISDeterministicBaseline && requiredTargetTrackIds.Count > 0;
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
        radarContacts = latestContacts;
        totalContacts = radarContacts.Count;
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

}
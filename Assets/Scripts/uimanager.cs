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

    [Header("Study Scenario")]
    public StudyScenario scenario = StudyScenario.RadarEOIRDegraded;
    public bool applyScenarioInstructionsOnStart = true;

    private Dictionary<string, GameObject> contactListItems = new Dictionary<string, GameObject>();
    private HashSet<string> confirmedTrackIds = new HashSet<string>();
    private Coroutine resetStatusCoroutine;

    void Start()
    {
        // Initialize UI by setting task instructions and setting contact status
        InitializeUI();

        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }
        // If auto-start is enabled, check for existing tracks and start task if any are found
        TryAutoStartTask();
    }

    void Update()
    {
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
        ApplyScenarioInstructions();
    }

    public bool IsEOIRAvailableForCurrentScenario()
    {
        return scenario != StudyScenario.AISDeterministicBaseline;
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

    private void TryAutoStartTask()
    {
        if (taskActive || !autoStartTaskWhenTracksAvailable)
        {
            return;
        }

        if (radarContacts != null && radarContacts.Count > 0)
        {
            StartTask(new List<Track>(radarContacts));
            return;
        }

        if (trackManager == null)
        {
            return;
        }

        List<Track> activeTracks = trackManager.GetActiveTracks();
        if (activeTracks != null && activeTracks.Count > 0)
        {
            StartTask(activeTracks);
        }
    }

    private void EnsureTaskStarted()
    {
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
        taskActive = true;
        taskStartTime = Time.time;
        confirmedContacts = 0;
        confirmedTrackIds.Clear();
        radarContacts = contacts;
        totalContacts = radarContacts.Count;

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
        // Clear existing contact list items
        foreach (var item in contactListItems.Values)
        {
            Destroy(item);
        }
        contactListItems.Clear();

        // Create new contact list items based on radarContacts
        foreach (Track track in radarContacts)
        {

            // Need a function to create a contact list item
            CreateContactListItem(track);
        }
    }

    public void CreateContactListItem(Track track)
    {
        if (contactListItemPrefab != null && contactListContent != null)
        {
            GameObject item = Instantiate(contactListItemPrefab, contactListContent);
            item.name = $"Contact_{track.trackid}";

            // Set contact details in the UI elements of the item
            TextMeshProUGUI idText = item.transform.Find("ContactIDText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI distText = item.transform.Find("DistanceText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI bearingText = item.transform.Find("BearingText")?.GetComponent<TextMeshProUGUI>();
            Image statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();

            if (idText != null) idText.text = track.trackid;
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
            if (statusIcon != null) statusIcon.color = Color.yellow;

            contactListItems[track.trackid] = item;
        }
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
            Image statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();

            if (statusIcon != null)
            {
                statusIcon.color = confirmed ? Color.green : Color.red;
            }
        }

        if (confirmed)
        {
            // Count each track only once, even if user captures it multiple times.
            if (confirmedTrackIds.Add(trackID))
            {
                confirmedContacts = confirmedTrackIds.Count;
            }
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
        if (contactsConfirmedText != null)
        {
            contactsConfirmedText.text = $"Confirmed: {confirmedContacts} / {totalContacts}";
        }
        
        if (progressBar != null && totalContacts > 0)
        {
            progressBar.value = (float)confirmedContacts / totalContacts;
        }

        // Check if task is complete and update status panel accordingly
        if (taskActive && totalContacts > 0 && confirmedContacts >= totalContacts)
        {
            // Need a function to set status panel to set task as complete
            CompleteTask();
        }
    }

    public void CompleteTask()
    {
        // Set task as inactive, record completion time, and update status panel to show completion
        taskActive = false;

        float completionTime = Time.time - taskStartTime;
        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);

        SetConfirmationStatus("TASK COMPLETE",
        $"All contacts confirmed in {minutes:00}:{seconds:00}", confirmedColor);

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
}
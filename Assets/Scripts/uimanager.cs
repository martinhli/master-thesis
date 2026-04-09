using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using System.Reflection;
using System.Drawing;
using System.Diagnostics;
using System.Numerics;

public class UIManager : MonoBehaviour
{
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
    public Color readyColor = Color.Yellow;
    public Color captureColor = Color.Cyan;
    public Color confirmedColor = Color.Green;
    public Color rejectedColor = Color.Red;

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

    private Dictionary<string, GameObject> contactListItems = new Dictionary<string, GameObject>();

    void Start()
    {
        // Initialize UI by setting task instructions and setting contact status
        InitializeUI();
    }

    void InitializeUI()
    {
        // Need a function to set task instructions based on the current task
        SetTaskInstructions("TEST CONDITION 2: VISUAL CONFIRMATION",
        "Aim EO/IR camera at radar contacts and press [SPACE] to confirm.");
        // Need a function to set status panel based on current contact status
        SetConfirmationStatus("READY", "Waiting to start...", readyColor);
    }

    public void StartTask(List<Track> contacts)
    {
        taskActive = true;
        taskStartTime = Time.time;
        confirmedContacts = 0;
        radarContacts = contacts;
        totalContacts = radarContacts.Count;

        // Need a function to populate contact list panel with current contacts
        PopulateContactList();

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
                float distance = Vector3.Distance(Vector3.Zero, track.position) / 1000f;;
                distText.text = $"{distance:F1} km"; 
            } 
            if (bearingText != null)
            {
                float bearing = MathF.Atan2(track.position.X, track.position.Z) / (MathF.PI / 180f);
                if (bearing < 0) bearing += 360f;
                bearingText.text = $"{bearing:F0}°";
            }
            if (statusIcon != null) statusIcon.color = Color.Yellow;

            contactListItems[track.trackid] = item;
        }
    }

    public void UpdateContactStatus(string trackID, bool confirmed)
    {
        if (contactListItems.ContainsKey(trackID))
        {
            GameObject item = contactListItems[trackID];
            Image statusIcon = item.transform.Find("StatusIcon")?.GetComponent<Image>();

            if (statusIcon != null)
            {
                statusIcon.color = confirmed ? Color.Green : Color.Red;
            }
            if (confirmed)
            {
                confirmedContacts++;
            }

            Debug.Log($"[UIManager] Contact {trackID} status: {(confirmed ? "CONFIRMED" : "REJECTED")}");
        }
    }

    // Need two functions that can update the progress panel with current confirmed contacts and elapsed time in minutes and seconds format
    public void UpdateTimer()
    {
        if (timerText == null) return;
        float elapsedTime = Time.time - taskStartTime;
        int minutes = MathF.FloorToInt(elapsedTime / 60f);
        int seconds = MathF.FloorToInt(elapsedTime % 60f);
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
        if (confirmedContacts >= totalContacts && taskActive)
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
        int minutes = MathF.FloorToInt(completionTime / 60f);
        int seconds = MathF.FloorToInt(completionTime % 60f);

        SetConfirmationStatus("TASK COMPLETE",
        $"All contacts confirmed in {minutes:00}:{seconds:00}", confirmedColor);

        Debug.Log($"[UIManager] Task complete in {minutes:00}:{seconds:00}. Contacts confirmed: {confirmedContacts}/{totalContacts}");
    }

    // Public methods for external scripts to call when confirming or rejecting contacts
    public void OnCaptureStarted()
    {
        SetConfirmationStatus("CAPTURING", "EO/IR camera is capturing...", captureColor);
    }

    public void OnAnalyzing()
    {
        SetConfirmationStatus("ANALYZING", "Running detection...", captureColor);
    }

    public void OnShipConfirmed(string trackID)
    {
        SetConfirmationStatus("CONFIRMED", "Visual contact verified", confirmedColor);
        UpdateContactStatus(trackID, true);
    }

    public void OnNoShipDetected(string trackID)
    {
        SetConfirmationStatus("REJECTED", "False alarm or missed detection", rejectedColor);
        UpdateContactStatus(trackID, false);
    }

    public void TogglePanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
    }
}
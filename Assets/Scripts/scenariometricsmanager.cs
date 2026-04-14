using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ScenarioMetricsManager : MonoBehaviour
{
    [Header("Collection Settings")]
    public float targetTimeLimitSeconds = 120f;
    public bool autoSaveCsvOnTaskEnd = true;
    public bool verboseLogging = true;
    public string outputFolderName = "StudyMetrics";
    public string outputFilePrefix = "scenario_metrics";

    [Header("Participant Metadata")]
    public string participantId = "P001";
    public bool persistParticipantCounter = true;
    public string participantIdPrefix = "P";
    [Range(1, 6)]
    public int participantIdPadding = 3;

    private class TargetState
    {
        public float firstSeenTime;
        public bool confirmed;
        public bool missed;
    }

    private readonly Dictionary<string, TargetState> targetStates = new Dictionary<string, TargetState>();
    private readonly List<float> completionTimes = new List<float>();
    private readonly List<string> csvRows = new List<string>();

    private bool taskRunning;
    private string currentScenario = "Unknown";
    private string sessionId = string.Empty;

    private int correctConfirmations;
    private int wrongSelections;
    private int wrongConfirmations;
    private int missedTargets;

    private const string ParticipantCounterPrefsKey = "ScenarioMetrics.ParticipantCounter";

    private void Awake()
    {
        sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        InitializeParticipantId();
        ResetCsvBuffer();
    }

    public void BeginTask(string scenarioName)
    {
        currentScenario = string.IsNullOrEmpty(scenarioName) ? "Unknown" : scenarioName;

        // Keep one clean CSV per task/scenario run.
        ResetCsvBuffer();

        taskRunning = true;
        targetStates.Clear();
        completionTimes.Clear();

        correctConfirmations = 0;
        wrongSelections = 0;
        wrongConfirmations = 0;
        missedTargets = 0;

        AddEvent("TASK_START", string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public void RegisterTargetAppearance(string trackId)
    {
        if (!taskRunning || string.IsNullOrEmpty(trackId))
        {
            return;
        }

        if (targetStates.ContainsKey(trackId))
        {
            return;
        }

        targetStates[trackId] = new TargetState
        {
            firstSeenTime = Time.time,
            confirmed = false,
            missed = false
        };

        AddEvent("TARGET_APPEARED", trackId, string.Empty, "0", string.Empty);
    }

    public void RegisterCorrectConfirmation(string trackId)
    {
        if (!taskRunning || string.IsNullOrEmpty(trackId))
        {
            return;
        }

        TargetState state;
        if (!targetStates.TryGetValue(trackId, out state))
        {
            state = new TargetState
            {
                firstSeenTime = Time.time,
                confirmed = false,
                missed = false
            };
            targetStates[trackId] = state;
        }

        if (state.confirmed)
        {
            return;
        }

        state.confirmed = true;
        float completionTime = Mathf.Max(0f, Time.time - state.firstSeenTime);
        completionTimes.Add(completionTime);
        correctConfirmations++;

        AddEvent("TARGET_CONFIRMED", trackId, string.Empty, completionTime.ToString("F3"), string.Empty);
    }

    public void RegisterWrongSelection(string selectedTrackId, string expectedTrackId)
    {
        if (!taskRunning)
        {
            return;
        }

        wrongSelections++;
        AddEvent("ERROR_WRONG_SELECTION", selectedTrackId, expectedTrackId, string.Empty, "wrong_target_selected");
    }

    public void RegisterWrongConfirmation(string trackId, string errorType)
    {
        if (!taskRunning)
        {
            return;
        }

        wrongConfirmations++;
        string normalizedError = string.IsNullOrEmpty(errorType) ? "wrong_confirmation" : errorType;
        AddEvent("ERROR_WRONG_CONFIRMATION", trackId, string.Empty, string.Empty, normalizedError);
    }

    public void EndTask(bool completed)
    {
        if (!taskRunning)
        {
            return;
        }

        // Final missed-target pass before closing the task.
        UpdateMissedTargets();

        taskRunning = false;

        int totalErrors = wrongSelections + wrongConfirmations;
        int totalAttempts = correctConfirmations + totalErrors;
        float errorRate = totalAttempts > 0 ? (float)totalErrors / totalAttempts : 0f;
        float avgCompletion = completionTimes.Count > 0 ? GetAverageCompletionTime() : 0f;

        AddEvent(
            completed ? "TASK_COMPLETE" : "TASK_END",
            string.Empty,
            string.Empty,
            avgCompletion.ToString("F3"),
            $"error_rate={errorRate:F3};missed={missedTargets}");

        if (verboseLogging)
        {
            Debug.Log($"[ScenarioMetricsCollector] Scenario={currentScenario}, AvgCompletion={avgCompletion:F2}s, Errors={totalErrors}, Missed={missedTargets}, ErrorRate={errorRate:P1}");
        }

        if (autoSaveCsvOnTaskEnd)
        {
            SaveCsv();
        }
    }

    private void Update()
    {
        if (!taskRunning)
        {
            return;
        }

        UpdateMissedTargets();
    }

    private void UpdateMissedTargets()
    {
        foreach (KeyValuePair<string, TargetState> pair in targetStates)
        {
            TargetState state = pair.Value;
            if (state.confirmed || state.missed)
            {
                continue;
            }

            float elapsed = Time.time - state.firstSeenTime;
            if (elapsed < targetTimeLimitSeconds)
            {
                continue;
            }

            state.missed = true;
            missedTargets++;
            AddEvent("TARGET_MISSED", pair.Key, string.Empty, elapsed.ToString("F3"), "timeout");
        }
    }

    private float GetAverageCompletionTime()
    {
        if (completionTimes.Count == 0)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < completionTimes.Count; i++)
        {
            sum += completionTimes[i];
        }

        return sum / completionTimes.Count;
    }

    private void AddEvent(string eventName, string trackId, string expectedTrackId, string value, string errorType)
    {
        string timestamp = DateTime.UtcNow.ToString("o");
        csvRows.Add(
            $"{EscapeCsv(timestamp)},{EscapeCsv(participantId)},{EscapeCsv(sessionId)},{EscapeCsv(currentScenario)},{EscapeCsv(eventName)},{EscapeCsv(trackId)},{EscapeCsv(expectedTrackId)},{EscapeCsv(value)},{EscapeCsv(errorType)}");
    }

    private void ResetCsvBuffer()
    {
        csvRows.Clear();
        csvRows.Add("timestamp_utc,participant_id,session_id,scenario,event,track_id,expected_track_id,value,error_type");
    }

    private void SaveCsv()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, outputFolderName);
            Directory.CreateDirectory(dir);

            string safeScenario = currentScenario.Replace(" ", "_").Replace("/", "-");
            string safeParticipant = (string.IsNullOrEmpty(participantId) ? "unknown" : participantId)
                .Trim()
                .Replace(" ", "_")
                .Replace("/", "-");
            string fileName = $"{outputFilePrefix}_{safeParticipant}_{sessionId}_{safeScenario}_{DateTime.UtcNow:HHmmss}.csv";
            string fullPath = Path.Combine(dir, fileName);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < csvRows.Count; i++)
            {
                sb.AppendLine(csvRows[i]);
            }

            File.WriteAllText(fullPath, sb.ToString());
            Debug.Log($"[ScenarioMetricsCollector] Metrics saved: {fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScenarioMetricsCollector] Failed to save CSV: {ex.Message}");
        }
    }

    private static string EscapeCsv(string raw)
    {
        if (raw == null)
        {
            return string.Empty;
        }

        if (!raw.Contains(",") && !raw.Contains("\"") && !raw.Contains("\n"))
        {
            return raw;
        }

        return $"\"{raw.Replace("\"", "\"\"")}\"";
    }

    public void SetParticipantId(string id)
    {
        participantId = string.IsNullOrWhiteSpace(id) ? "unknown" : id.Trim();

        if (persistParticipantCounter)
        {
            int parsedCounter;
            if (TryExtractParticipantNumber(participantId, out parsedCounter) && parsedCounter > 0)
            {
                PlayerPrefs.SetInt(ParticipantCounterPrefsKey, parsedCounter);
                PlayerPrefs.Save();
            }
        }
    }

    public void AdvanceParticipantId()
    {
        IncrementParticipantId();
    }

    private void InitializeParticipantId()
    {
        int counter = 0;

        if (persistParticipantCounter)
        {
            counter = PlayerPrefs.GetInt(ParticipantCounterPrefsKey, 0);
        }

        if (counter <= 0)
        {
            int parsedCounter;
            if (TryExtractParticipantNumber(participantId, out parsedCounter) && parsedCounter > 0)
            {
                counter = parsedCounter;
            }
        }

        if (counter <= 0)
        {
            counter = 1;
        }

        participantId = BuildParticipantId(counter);

        if (persistParticipantCounter)
        {
            PlayerPrefs.SetInt(ParticipantCounterPrefsKey, counter);
            PlayerPrefs.Save();
        }
    }

    private void IncrementParticipantId()
    {
        int current = 0;
        int parsed;
        if (TryExtractParticipantNumber(participantId, out parsed))
        {
            current = parsed;
        }

        int next = Mathf.Max(1, current + 1);
        participantId = BuildParticipantId(next);

        if (persistParticipantCounter)
        {
            PlayerPrefs.SetInt(ParticipantCounterPrefsKey, next);
            PlayerPrefs.Save();
        }

        if (verboseLogging)
        {
            Debug.Log($"[ScenarioMetricsCollector] Next participant ID prepared: {participantId}");
        }
    }

    private string BuildParticipantId(int counter)
    {
        string prefix = string.IsNullOrEmpty(participantIdPrefix) ? "P" : participantIdPrefix;
        int padding = Mathf.Clamp(participantIdPadding, 1, 6);
        return $"{prefix}{counter.ToString($"D{padding}")}";
    }

    private bool TryExtractParticipantNumber(string id, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        int end = id.Length - 1;
        while (end >= 0 && char.IsDigit(id[end]))
        {
            end--;
        }

        if (end == id.Length - 1)
        {
            return false;
        }

        string numericPart = id.Substring(end + 1);
        return int.TryParse(numericPart, out number);
    }
}

using Data;
using UnityEngine;
using System.Collections.Generic;

public class StudyScenarioController : MonoBehaviour
{
    [Header("Scenario")]
    public UIManager.StudyScenario selectedScenario = UIManager.StudyScenario.AISDeterministicBaseline;
    public bool applyOnStart = true;
    public bool clearTracksOnScenarioChange = true;

    [Header("Unknown Contact Tasking")]
    [Tooltip("In Scenarios 2 and 3, only these ship types are treated as unknown contacts")]
    public bool useTypeBasedUnknownContacts = true;

    [Tooltip("Ship types considered unknown contacts for operator identification tasks")]
    public List<SimulatedShip.ShipType> unknownContactShipTypes = new List<SimulatedShip.ShipType>
    {
        SimulatedShip.ShipType.Cargo,
        SimulatedShip.ShipType.Passenger
    };

    [Tooltip("Treat Military ships as unknown contacts in Scenario 2 only")]
    public bool includeMilitaryAsUnknownInScenario2 = true;

    [Header("References")]
    public UIManager uiManager;
    public SensorSimulator sensorSimulator;
    public TrackManager trackManager;
    public EOIRCameraController eoirCameraController;

    [Header("EO/IR Controller Runtime")]
    [Tooltip("Keep EOIRCameraController script enabled in Scenario 1 so left-controller ray/confirm input still runs")]
    public bool keepEOIRControllerEnabledInScenario1 = true;

    [Header("Optional Scenario UI/Objects")]
    [Tooltip("Parent/root object that contains AIS-based overlay visuals")]
    public GameObject aisOverlayRoot;

    [Tooltip("Parent/root object that contains radar/fused track overlays")]
    public GameObject radarOverlayRoot;

    void Start()
    {
        AutoResolveReferences();

        if (uiManager != null)
        {
            selectedScenario = uiManager.scenario;
        }

        if (applyOnStart)
        {
            ApplySelectedScenario();
        }
    }

    [ContextMenu("Apply Selected Scenario")]
    public void ApplySelectedScenario()
    {
        ApplyScenario(selectedScenario);
    }

    public void ApplyScenario(UIManager.StudyScenario scenario)
    {
        selectedScenario = scenario;

        bool enableAIS = scenario == UIManager.StudyScenario.AISDeterministicBaseline ||
                         scenario == UIManager.StudyScenario.FusedUncertaintyAware;
        bool enableRadar = scenario == UIManager.StudyScenario.RadarEOIRDegraded ||
                           scenario == UIManager.StudyScenario.FusedUncertaintyAware;
        bool enableEOIR = scenario == UIManager.StudyScenario.RadarEOIRDegraded ||
                          scenario == UIManager.StudyScenario.FusedUncertaintyAware;
        bool enableEOIRController = enableEOIR ||
                        (keepEOIRControllerEnabledInScenario1 && scenario == UIManager.StudyScenario.AISDeterministicBaseline);

        if (sensorSimulator != null)
        {
            sensorSimulator.aisEnabled = enableAIS;
            sensorSimulator.radarEnabled = enableRadar;
            sensorSimulator.eoirEnabled = enableEOIR;
        }

        if (eoirCameraController != null)
        {
            eoirCameraController.enabled = enableEOIRController;
        }

        if (aisOverlayRoot != null)
        {
            aisOverlayRoot.SetActive(enableAIS);
        }

        if (radarOverlayRoot != null)
        {
            radarOverlayRoot.SetActive(enableRadar);
        }

        switch (scenario)
        {
            case UIManager.StudyScenario.AISDeterministicBaseline:
                tracklabeler.ActiveDisplayMode = tracklabeler.LabelDisplayMode.AISDeterministicIdentity;
                break;

            case UIManager.StudyScenario.RadarEOIRDegraded:
                tracklabeler.ActiveDisplayMode = tracklabeler.LabelDisplayMode.RadarUncertainIdentity;
                break;

            case UIManager.StudyScenario.FusedUncertaintyAware:
                tracklabeler.ActiveDisplayMode = tracklabeler.LabelDisplayMode.FusedUncertaintyAwareIdentity;
                break;
        }

        ConfigureUnknownContactFlags(scenario);

        if (uiManager != null)
        {
            uiManager.ConfigureScenario(scenario);
            uiManager.PopulateContactList();
            uiManager.UpdateProgress();
        }

        if (clearTracksOnScenarioChange && trackManager != null)
        {
            trackManager.ClearAllTracks();
        }

        trackoverlayer overlay = FindFirstObjectByType<trackoverlayer>();
        if (overlay != null)
        {
            overlay.onlyShowUnknownTargetsInIdentityTaskModes = false;
            overlay.ClearAllLabels();
        }

        Debug.Log($"[StudyScenarioController] Applied scenario: {scenario} (AIS={enableAIS}, Radar={enableRadar}, EO/IR Sensors={enableEOIR}, EO/IR Controller={enableEOIRController})");
    }

    private void AutoResolveReferences()
    {
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }

        if (sensorSimulator == null)
        {
            sensorSimulator = FindFirstObjectByType<SensorSimulator>();
        }

        if (trackManager == null)
        {
            trackManager = FindFirstObjectByType<TrackManager>();
        }

        if (eoirCameraController == null)
        {
            eoirCameraController = FindFirstObjectByType<EOIRCameraController>();
        }
    }

    private void ConfigureUnknownContactFlags(UIManager.StudyScenario scenario)
    {
        if (!useTypeBasedUnknownContacts)
        {
            return;
        }

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        bool useUnknownTaskMode = scenario == UIManager.StudyScenario.RadarEOIRDegraded ||
                                  scenario == UIManager.StudyScenario.FusedUncertaintyAware;

        foreach (SimulatedShip ship in ships)
        {
            if (ship == null)
            {
                continue;
            }

            bool isScenario2 = scenario == UIManager.StudyScenario.RadarEOIRDegraded;
            bool isTaskUnknown = useUnknownTaskMode && (
                unknownContactShipTypes.Contains(ship.shipType) ||
                (isScenario2 && includeMilitaryAsUnknownInScenario2 && ship.shipType == SimulatedShip.ShipType.Military));
            ship.aisTransponder = !isTaskUnknown;
        }
    }
}

using UnityEngine;
using TMPro;   // or TMPro if you use TextMeshPro
using Data;

public class ShipInfoHolder : MonoBehaviour
{
    [Header("Data")]
    public Ship shipData;

    [Header("UI References")]
    public TMP_Text vesselNameText;
    public TMP_Text imoText;
    public TMP_Text mmsiText;
    public TMP_Text speedText;
    public TMP_Text courseText;

    public void SetShipData(Ship ship)
    {
        shipData = ship;
        if (ship == null)
        {
            Debug.LogWarning("ShipInfoHolder.SetShipData called with null ship");
            return;
        }

        Debug.Log($"ShipInfoHolder.SetShipData: name={ship.name}, IMO={ship.imo}, MMSI={ship.mmsi}, " +
                  $"speed={ship.speed}, course={ship.course}");

        if (vesselNameText != null)
            vesselNameText.text = string.IsNullOrEmpty(ship.name) ? "Unknown Vessel" : ship.name;

        if (imoText != null)
            imoText.text = $"IMO: {ship.imo}";

        if (mmsiText != null)
            mmsiText.text = $"MMSI: {ship.mmsi}";

        if (speedText != null)
            speedText.text = $"{ship.speed:F1} kn";

        if (courseText != null)
            courseText.text = $"{ship.course:F0}°";
    }
}

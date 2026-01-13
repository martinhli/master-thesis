using UnityEngine;
using TMPro;   // or TMPro if you use TextMeshPro
using Data;

public class ShipAnnotationView : MonoBehaviour
{
    public TMPro.TextMeshProUGUI title;
    public TMPro.TextMeshProUGUI imo;
    public TMPro.TextMeshProUGUI speedCourse;

    public void Bind(Data.Ship s)
    {
        if (s == null) return;

        Debug.Log($"ShipAnnotationView.Bind: name={s.name}, IMO={s.imo}, speed={s.speed}, course={s.course}");

        if (title != null)
            title.text = string.IsNullOrEmpty(s.name) ? "Unknown" : s.name;

        if (imo != null)
            imo.text = $"IMO: {s.imo}";

        if (speedCourse != null)
            speedCourse.text = $"SOG {s.speed:F1} kn  COG {s.course:F0}°";
    }
}

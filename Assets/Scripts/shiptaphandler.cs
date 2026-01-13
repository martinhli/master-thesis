using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ShipTapHandler : MonoBehaviour, IPointerClickHandler
{
    [Header("Assign the SOG/COG TMP component here")]
    public TextMeshProUGUI sogcog;

    [Header("Behavior")]
    public bool toggle = true;            // tap toggles on/off
    public float autoHideSeconds = 0f;    // set > 0 to auto-hide after N seconds

    Coroutine hideRoutine;

    void Awake()
    {
        // Start hidden by default
        if (sogcog) sogcog.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!sogcog) return;

        if (toggle)
        {
            bool show = !sogcog.gameObject.activeSelf;
            sogcog.gameObject.SetActive(show);
            RestartAutoHide(show);
        }
        else
        {
            sogcog.gameObject.SetActive(true);
            RestartAutoHide(true);
        }
    }

    void RestartAutoHide(bool showing)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        if (showing && autoHideSeconds > 0f)
            hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(autoHideSeconds);
        if (sogcog) sogcog.gameObject.SetActive(false);
    }
}
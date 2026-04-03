using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EOIRMinimap : MonoBehaviour
{
    public EOIRFootprintTracker tracker;
    public RawImage minimapImage;

    [Header("Ship Icons")]
    [Tooltip("Sprite used for each ship symbol (for example a circle or triangle)")]
    public Sprite shipIconSprite;

    [Tooltip("Tint applied to each ship icon")]
    public Color shipIconColor = Color.white;

    [Tooltip("Parent under which ship icons are created. Defaults to the minimap image rect")]
    public RectTransform shipIconsRoot;

    [Tooltip("Pixels per ship icon")]
    public float shipIconSize = 18f;

    [Tooltip("Hide ship icons that fall outside the minimap bounds")]
    public bool hideShipsOutsideMap = true;

    [Tooltip("Scale ship icon rotation using ship course/heading")]
    public bool rotateIconsToShipHeading = true;

    [Tooltip("Optional ownship heading icon on top of minimap")]
    public RectTransform headingIcon;

    [Tooltip("Optional Image to apply a heading sprite to")]
    public Image headingIconImage;

    [Tooltip("Optional sprite for the heading arrow")]
    public Sprite headingIconSprite;

    [Tooltip("Transform used to compute heading arrow rotation")]
    public Transform headingSource;

    private readonly Dictionary<SimulatedShip, RectTransform> _shipIcons = new Dictionary<SimulatedShip, RectTransform>();

    void Start()
    {
        if (shipIconsRoot == null && minimapImage != null)
        {
            shipIconsRoot = minimapImage.rectTransform;
        }

        if (headingIconImage != null && headingIconSprite != null)
        {
            headingIconImage.sprite = headingIconSprite;
            headingIconImage.color = Color.white;
        }
    }

    void Update()
    {
        if (tracker != null && minimapImage != null && tracker.CoverageTexture != null)
        {
            if (minimapImage.texture != tracker.CoverageTexture)
            {
                minimapImage.texture = tracker.CoverageTexture;
            }
        }

        UpdateHeadingIcon();
        UpdateShipIcons();
    }

    void UpdateShipIcons()
    {
        if (tracker == null || shipIconSprite == null || shipIconsRoot == null)
        {
            return;
        }

        SimulatedShip[] ships = FindObjectsOfType<SimulatedShip>();
        HashSet<SimulatedShip> aliveShips = new HashSet<SimulatedShip>();

        for (int i = 0; i < ships.Length; i++)
        {
            SimulatedShip ship = ships[i];
            if (ship == null)
            {
                continue;
            }

            aliveShips.Add(ship);

            RectTransform icon;
            if (!_shipIcons.TryGetValue(ship, out icon) || icon == null)
            {
                GameObject iconObject = new GameObject($"ShipIcon_{ship.shipName}", typeof(RectTransform), typeof(Image));
                icon = iconObject.GetComponent<RectTransform>();
                icon.SetParent(shipIconsRoot, false);
                icon.anchorMin = new Vector2(0.5f, 0.5f);
                icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);

                Image image = iconObject.GetComponent<Image>();
                image.sprite = shipIconSprite;
                image.color = shipIconColor;
                image.preserveAspect = true;
                image.raycastTarget = false;

                icon.name = $"ShipIcon_{ship.shipName}";
                icon.gameObject.SetActive(true);
                _shipIcons[ship] = icon;
            }

            UpdateShipIconTransform(ship, icon);
        }

        CleanupDestroyedShips(aliveShips);
    }

    void UpdateShipIconTransform(SimulatedShip ship, RectTransform icon)
    {
        if (ship == null || icon == null || tracker == null)
        {
            return;
        }

        float mapSize = Mathf.Max(1f, tracker.mapSizeMeters);
        Transform mapCenter = tracker.mapCenter;
        Vector3 centerPos = mapCenter != null ? mapCenter.position : Vector3.zero;

        Vector3 rel = ship.transform.position - centerPos;
        float u = (rel.x / mapSize) + 0.5f;
        float v = (rel.z / mapSize) + 0.5f;

        bool inBounds = u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        if (hideShipsOutsideMap && !inBounds)
        {
            if (icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(false);
            }
            return;
        }

        if (!icon.gameObject.activeSelf)
        {
            icon.gameObject.SetActive(true);
        }

        RectTransform rootRect = shipIconsRoot;
        float width = rootRect.rect.width;
        float height = rootRect.rect.height;

        float x = (u - 0.5f) * width;
        float y = (v - 0.5f) * height;
        icon.anchoredPosition = new Vector2(x, y);
        icon.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, shipIconSize);
        icon.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, shipIconSize);

        if (rotateIconsToShipHeading)
        {
            float heading = ship.GetCourse();
            icon.localRotation = Quaternion.Euler(0f, 0f, -heading);
        }
    }

    void CleanupDestroyedShips(HashSet<SimulatedShip> aliveShips)
    {
        List<SimulatedShip> toRemove = null;

        foreach (KeyValuePair<SimulatedShip, RectTransform> pair in _shipIcons)
        {
            if (pair.Key == null || !aliveShips.Contains(pair.Key) || pair.Value == null)
            {
                if (toRemove == null)
                {
                    toRemove = new List<SimulatedShip>();
                }

                toRemove.Add(pair.Key);
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _shipIcons.Remove(toRemove[i]);
        }
    }

    void UpdateHeadingIcon()
    {
        if (headingIcon == null || headingSource == null)
        {
            return;
        }

        Vector3 planarForward = headingSource.forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        planarForward.Normalize();
        float headingDeg = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
        headingIcon.localRotation = Quaternion.Euler(0f, 0f, -headingDeg);
    }
}

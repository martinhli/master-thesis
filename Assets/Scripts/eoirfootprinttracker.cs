using UnityEngine;

public class EOIRFootprintTracker : MonoBehaviour
{
    [Header("References")]
    public Camera eoirCamera;
    public Transform mapCenter;

    [Header("Map Settings")]
    public float seaLevelY = 0f;
    public float mapSizeMeters = 20000f;
    public int resolution = 256;
    public float updateInterval = 0.25f;

    [Header("Colors")]
    public Color32 unsearched = new Color32(10, 20, 30, 180);
    public Color32 searched = new Color32(40, 120, 100, 220);
    public Color32 current = new Color32(90, 230, 255, 255);

    public Texture2D CoverageTexture => _texture;

    private Texture2D _texture;
    private Color32[] _historyPixels;
    private Color32[] _framePixels;
    private float _nextTick;

    void Start()
    {
        if (resolution < 16)
        {
            resolution = 16;
        }

        _texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        _texture.wrapMode = TextureWrapMode.Clamp;
        _texture.filterMode = FilterMode.Bilinear;

        _historyPixels = new Color32[resolution * resolution];
        _framePixels = new Color32[resolution * resolution];
        for (int i = 0; i < _historyPixels.Length; i++)
        {
            _historyPixels[i] = unsearched;
            _framePixels[i] = unsearched;
        }

        _texture.SetPixels32(_framePixels);
        _texture.Apply(false, false);
    }

    void Update()
    {
        if (eoirCamera == null || mapCenter == null)
        {
            return;
        }

        if (Time.time < _nextTick)
        {
            return;
        }
        _nextTick = Time.time + Mathf.Max(0.02f, updateInterval);

        Vector2 uv0;
        Vector2 uv1;
        Vector2 uv2;
        Vector2 uv3;
        if (!TryGetFootprintUV(out uv0, out uv1, out uv2, out uv3))
        {
            ComposeAndUpload(false, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            return;
        }

        FillQuad(_historyPixels, uv0, uv1, uv2, uv3, searched);
        ComposeAndUpload(true, uv0, uv1, uv2, uv3);
    }

    public void ResetCoverage()
    {
        if (_historyPixels == null)
        {
            return;
        }

        for (int i = 0; i < _historyPixels.Length; i++)
        {
            _historyPixels[i] = unsearched;
            _framePixels[i] = unsearched;
        }

        _texture.SetPixels32(_framePixels);
        _texture.Apply(false, false);
    }

    bool TryGetFootprintUV(out Vector2 uv0, out Vector2 uv1, out Vector2 uv2, out Vector2 uv3)
    {
        uv0 = Vector2.zero;
        uv1 = Vector2.zero;
        uv2 = Vector2.zero;
        uv3 = Vector2.zero;

        Vector3 w0;
        Vector3 w1;
        Vector3 w2;
        Vector3 w3;
        if (!TryGetFootprintWorld(out w0, out w1, out w2, out w3))
        {
            return false;
        }

        uv0 = WorldToUV(w0);
        uv1 = WorldToUV(w1);
        uv2 = WorldToUV(w2);
        uv3 = WorldToUV(w3);
        return true;
    }

    bool TryGetFootprintWorld(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        p0 = Vector3.zero;
        p1 = Vector3.zero;
        p2 = Vector3.zero;
        p3 = Vector3.zero;

        Vector2[] corners = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        Vector3[] points = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            Ray ray = eoirCamera.ViewportPointToRay(new Vector3(corners[i].x, corners[i].y, 0f));
            float denom = ray.direction.y;
            if (Mathf.Abs(denom) < 0.00001f)
            {
                return false;
            }

            float t = (seaLevelY - ray.origin.y) / denom;
            if (t <= 0f)
            {
                return false;
            }

            points[i] = ray.origin + ray.direction * t;
        }

        p0 = points[0];
        p1 = points[1];
        p2 = points[2];
        p3 = points[3];
        return true;
    }

    Vector2 WorldToUV(Vector3 worldPoint)
    {
        float half = mapSizeMeters * 0.5f;
        Vector3 rel = worldPoint - mapCenter.position;
        float u = Mathf.Clamp01((rel.x + half) / mapSizeMeters);
        float v = Mathf.Clamp01((rel.z + half) / mapSizeMeters);
        return new Vector2(u, v);
    }

    void ComposeAndUpload(bool drawCurrent, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        System.Array.Copy(_historyPixels, _framePixels, _historyPixels.Length);
        if (drawCurrent)
        {
            FillQuad(_framePixels, a, b, c, d, current);
        }

        _texture.SetPixels32(_framePixels);
        _texture.Apply(false, false);
    }

    void FillQuad(Color32[] pixels, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        FillTriangle(pixels, a, b, c, color);
        FillTriangle(pixels, a, c, d, color);
    }

    void FillTriangle(Color32[] pixels, Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * (resolution - 1)), 0, resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * (resolution - 1)), 0, resolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * (resolution - 1)), 0, resolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * (resolution - 1)), 0, resolution - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));
                if (IsInsideTriangle(p, a, b, c))
                {
                    pixels[y * resolution + x] = color;
                }
            }
        }
    }

    bool IsInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Cross2D(b - a, p - a);
        float s2 = Cross2D(c - b, p - b);
        float s3 = Cross2D(a - c, p - c);

        bool hasNeg = s1 < 0f || s2 < 0f || s3 < 0f;
        bool hasPos = s1 > 0f || s2 > 0f || s3 > 0f;
        return !(hasNeg && hasPos);
    }

    float Cross2D(Vector2 x, Vector2 y)
    {
        return x.x * y.y - x.y * y.x;
    }
}
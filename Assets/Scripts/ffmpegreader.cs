using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Data;

public class FFmpegReader : MonoBehaviour
{
    [Header("Stream Settings")]
    public string streamUrl = "Assets/TestData/TestData/EOIR_video_11-40/EON.ts";// Placeholder URL, gets supplied from videostream
    public Renderer videoRenderer; // Assign to a Unity Quad or RawImage
    public AISOverlay overlaySystem;
    public dataparser parser;
    public AISData aisData;

    private KLVFrameMetadata _lastMetadata;
    
    private IntPtr _ctx = IntPtr.Zero;
    private Texture2D _tex;
    
    private int _width;
    private int _height;

    private byte[] _rgb;
    private GCHandle _rgbHandle;
    private IntPtr _rgbPtr;
    
    private byte[] _klvBuffer = new byte[4096]; // adjust if needed
    private GCHandle _klvHandle;
    private IntPtr _klvPtr;

    
    void Start()
    {
        if (videoRenderer == null)
        {
            Debug.LogError("FFmpegReader: videoRenderer not assigned", this);
            return;
        }
        if (parser == null)
        {
            Debug.LogError("FFmpegReader: parser not assigned", this);
            return;
        }
        TextAsset aisAsset = Resources.Load<TextAsset>("aisdata"); // file: Assets/Resources/aisdata.json
        if (aisAsset == null)
        {
            Debug.LogError("FFmpegReader: could not find aisdata.json in Resources");
        }
        else
        {
            aisData = parser.parseAISData(aisAsset.text);
            int count = aisData?.ships != null ? aisData.ships.Count : 0;
            Debug.Log($"FFmpegReader: loaded AIS data with {count} ship(s).");
        }


        Debug.Log("FFWrap version: " + FFmpegNative.Version);
        // Resolve relative asset paths to absolute paths
        if (!streamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !Path.IsPathRooted(streamUrl))
        {
            var trimmed = streamUrl.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? streamUrl.Substring("Assets/".Length) : streamUrl;

            string candidate = Path.Combine(Application.dataPath, trimmed.TrimStart('/', '\\'));
            if (File.Exists(candidate))
            {
                streamUrl = candidate;
                Debug.Log($"FFmpegReader: resolved streamUrl to '{streamUrl}'");
            }
            else
            {
                Debug.LogWarning($"FFmpegReader: streamUrl not found at '{candidate}'. Using original value '{streamUrl}'.");
            }
        }

        int rc = FFmpegNative.ffw_open(streamUrl, out _ctx, out _width, out _height);
        Debug.Log($"ffw_open rc={rc}, size={_width}x{_height}");

        if (rc < 0 || _ctx == IntPtr.Zero)
        {
            Debug.LogError($"ffw_open failed with code {rc}");
            return;
        }

        _tex = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
        videoRenderer.material.mainTexture = _tex;

        _rgb = new byte[_width * _height * 4];
        _rgbHandle = GCHandle.Alloc(_rgb, GCHandleType.Pinned);
        _rgbPtr = _rgbHandle.AddrOfPinnedObject();

        _klvHandle = GCHandle.Alloc(_klvBuffer, GCHandleType.Pinned);
        _klvPtr = _klvHandle.AddrOfPinnedObject();

        // Example: poll in Update; you can also use InvokeRepeating
        InvokeRepeating(nameof(Poll), 0.0f, 0.03f);
    }

   void Poll()
   {
        if (_ctx == IntPtr.Zero)
            return;

        int rc = FFmpegNative.ffw_read_next(
            _ctx,
            out var kind,
            _rgbPtr, _width * 4,
            out int w, out int h,
            _klvPtr, _klvBuffer.Length, out int klvLen,
            out long pts);

        if (rc == 0)
        {
            // EOF
            Debug.Log("ffw_read_next: EOF");
            CancelInvoke(nameof(Poll));
            return;
        }
        if (rc < 0)
        {
            Debug.LogWarning($"ffw_read_next error {rc}");
            return;
        }

        if (kind == FFmpegNative.SampleKind.Video && w > 0 && h > 0)
        {
            // Update texture
            _tex.LoadRawTextureData(_rgb);
            _tex.Apply();

            if (aisData != null && _lastMetadata != null)
            {
                parser.SyncData(aisData, _lastMetadata);
            }
        }
        else if (kind == FFmpegNative.SampleKind.KLV && klvLen > 0)
        {
            byte[] klvData = new byte[klvLen];
            Buffer.BlockCopy(_klvBuffer, 0, klvData, 0, klvLen);

            var metadata = parser.parseKLVMetadata(klvData);
            if (metadata != null)
            {
                _lastMetadata = metadata;
                Debug.Log(
                    $"KLV parsed: ts={metadata.unixTimestamp}, " +
                    $"Lat={metadata.sensorLat}, Lon={metadata.sensorLon}, " +
                    $"FOV={metadata.fovHorizontal}");

                if (MetricsManager.Instance != null && MetricsManager.Instance.metricsEnabled)
                {
                    MetricsManager.Instance.OnMetadataReceived(metadata);
                }
                else
                {
                    Debug.LogWarning("[Metrics] Instance is null or metrics disabled when KLV metadata arrived.");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (_rgbHandle.IsAllocated) _rgbHandle.Free();
        if (_klvHandle.IsAllocated) _klvHandle.Free();
        if (_ctx != IntPtr.Zero) FFmpegNative.ffw_close(ref _ctx);
    }  
}
    
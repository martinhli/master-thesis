using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Numerics;
using System.Diagnostics;
using System.Data;

namespace Data
{
    [Serializable]
    public class EOIRMetadata
    {
        public string timestamp;
        public GeoPosition cameraPosition;
        public Orientation cameraOrientation;
        public FOV fov;
    }

    [Serializable]
    public class KLVFrameMetadata
    {
        public long unixTimestamp;
        public double platformHeading;
        public double platformPitch;
        public double platformRoll;

        public double sensorLat;
        public double sensorLon;
        public double sensorAlt;

        public double fovHorizontal;
        public double fovVertical;

        public double azimuth;
        public double elevation;
        public double roll;

        public int imageWidth;
        public int imageHeight;

        public double frameCenterLat;
        public double frameCenterLon;
        public double frameCenterElevation;
    }

    [Serializable]
    public class GeoPosition
    {
        public double lon;
        public double lat;
    }

    [Serializable]
    public class Orientation
    {
        public float azimuth;
        public float elevation;
        public float roll;
    }

    [Serializable]
    public class FOV
    {
        public float horizontal;
        public float vertical;
    }

    [Serializable]
    public class AISData
    {
        public string timestamp;
        public List<Ship> ships;
    }

    [Serializable]
    public class Ship
    {
        public string name;
        public string imo;
        public string mmsi;
        public float lat;
        public float lon;
        public float course;
        public float speed;
    }

    [Serializable]
    public class Track
    {
        public string trackid { get; set;}

        public Vector3 position { get; set;}
        public Vector3 velocity { get; set;}

        public Sensor sources { get; set;}

        public IdentityConfidence identityConfidence { get; set;}

        public DateTime timeStamp { get; set;}

        public TrackState state {get; set;}

        public Track()
        {
            trackid = Guid.NewGuid().ToString();
            sources = new Sensor();
            identityConfidence = IdentityConfidence.None;
            timeStamp = DateTime.UtcNow;
            state = TrackState.Predicted;
        }

        public void UpdateTrack(Vector3 newposition, Vector3 newvelocity, Sensor newsources, IdentityConfidence newidentityConfidence, TrackState newstate)
        {
            position = newposition;
            velocity = newvelocity;
            sources = newsources;
            identityConfidence = newidentityConfidence;
            timeStamp = DateTime.UtcNow;
            state = newstate;
        }
    }

    [Flags]
    public enum SensorType
    {
        None = 0,
        AIS = 1 << 0,
        Radar = 1 << 1,
        EOIR = 1 << 2
    }

    public class Sensor
    {
        public SensorType activeSensors { get; set;}

        public void addSensor(SensorType sensor)
        {
            activeSensors |= sensor;
        }

        public void removeSensor(SensorType sensor)
        {
            activeSensors &= ~sensor;
        }

        public bool hasSensor(SensorType sensor)
        {
            return (activeSensors & sensor) == sensor;
        }

        public bool hasMultipleSensors()
        {
            return (activeSensors & (activeSensors - 1)) != 0;
        }
    }

    public enum IdentityConfidence
    {
        None, // No identity information
        Low, // Single sensor, Weak confidence
        Medium, // Single sensor, Strong confidence
        High, // Multiple sensors, good correlation with AIS data

        Strong // Multiple sensors, strong correlation with AIS data
    }

    public enum TrackState
    {
        Predicted, // Extrapolated from observation
        Observed, // Detected by sensor but not yet confirmed
        Confirmed // Confirmed track
    }

    public class dataparser : MonoBehaviour
    {
        public AISOverlay overlaySystem;

        public EOIRMetadata parseEOIRMetadata(string json)
        {
            return JsonUtility.FromJson<EOIRMetadata>(json);
        }

        public AISData parseAISData(string json)
        {
            return JsonUtility.FromJson<AISData>(json);
        }

        public KLVFrameMetadata parseKLVMetadata(byte[] klvData)
        {
            if (klvData == null || klvData.Length < 2)
                return null;

            klvData = RemoveMISBPrefix(klvData);    

            var metadata = new KLVFrameMetadata();
            bool receivedData = false;

            using (var ms = new MemoryStream(klvData))
            using (var reader = new BinaryReader(ms))
            {
                int itemIndex = 0;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    // Need at least 2 bytes for tag + length
                    if (reader.BaseStream.Length - reader.BaseStream.Position < 2)
                        break;

                    byte tag = reader.ReadByte();
                    int length = reader.ReadByte();

                    // If declared length runs past the end, stop the process
                    if (length <= 0 || reader.BaseStream.Position + length > reader.BaseStream.Length)
                    {
                        Debug.LogWarning($"KLV invalid length {length} for tag {tag} at item {itemIndex}");
                        break;
                    }

                    byte[] valueBytes = reader.ReadBytes(length);

                    // DEBUG: log first few items so we see what's actually in the stream
                    if (itemIndex < 20)  // don't spam too much
                    {
                        string hex = BitConverter.ToString(valueBytes, 0, Math.Min(valueBytes.Length, 8));
                        Debug.Log($"KLV item {itemIndex}: tag={tag} len={length} bytes={hex}");
                    }
                    itemIndex++;

                    try
                    {
                        switch (tag)
                        {
                            case 2: // timestamp
                                if (valueBytes.Length >= 2)
                                {
                                    metadata.unixTimestamp = BytesToInt64(valueBytes);
                                    receivedData = true;
                                }
                                break;

                            case 5: // platform heading
                                metadata.platformHeading = BytesToScaledDouble(valueBytes, 0.0, 360.0);
                                receivedData = true;
                                break;
                            case 6: // platform pitch
                                metadata.platformPitch = BytesToScaledDouble(valueBytes, -90.0, 90.0);
                                receivedData = true;
                                break;
                            case 7: // platform roll
                                metadata.platformRoll = BytesToScaledDouble(valueBytes, -180.0, 180.0);
                                receivedData = true;
                                break;
                            case 13: // sensor latitude
                                metadata.sensorLat = ScaleSignedInt(valueBytes, -90.0, 90.0);
                                receivedData = true;
                                break;
                            case 14: // sensor longitude
                                metadata.sensorLon = ScaleSignedInt(valueBytes, -180.0, 180.0);
                                receivedData = true;
                                break;
                            case 15:  // Sensor altitude
                                metadata.sensorAlt = BytesToScaledDouble(valueBytes, -900.0, 19000.0);
                                receivedData = true;
                                break;
                            case 16: // Horizontal FOV
                                metadata.fovHorizontal = BytesToScaledDouble(valueBytes, 0.0, 180.0);
                                receivedData = true;
                                break;
                            case 17: // Vertical FOV
                                metadata.fovVertical = BytesToScaledDouble(valueBytes, 0.0, 180.0);
                                receivedData = true;
                                break;
                            case 18: // Sensor azimuth
                                metadata.azimuth = BytesToScaledDouble(valueBytes, 0.0, 360.0);
                                receivedData = true;
                                break;
                            case 19: // Sensor elevation
                                metadata.elevation = BytesToScaledDouble(valueBytes, 0.0, 90.0);
                                receivedData = true;
                                break;
                            case 20: // Sensor roll
                                metadata.roll = BytesToScaledDouble(valueBytes, -180.0, 180.0);
                                receivedData = true;
                                break;
                            case 23: // Frame center lat
                                metadata.frameCenterLat = ScaleSignedInt(valueBytes, -90.0, 90.0);
                                receivedData = true;
                                break;
                            case 24: // Frame center lon
                                metadata.frameCenterLon = ScaleSignedInt(valueBytes, -180.0, 180.0);
                                receivedData = true;
                                break;
                            default:
                                // ignore other tags
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"KLV parse: failed to parse tag {tag} (len={valueBytes.Length}): {ex.Message}");
                    }
                }
            }

            if (metadata != null && MetricsManager.Instance != null)
            {
                MetricsManager.Instance.OnMetadataReceived(metadata);
            }

            if (!receivedData)
            {
                return null;
            }

            return metadata;
        }

        private long BytesToInt64(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return 0;

            byte[] padded = new byte[8];
            int srcLen = Math.Min(bytes.Length, 8);

            Buffer.BlockCopy(bytes, bytes.Length - srcLen, padded, 8 - srcLen, srcLen);

            Array.Reverse(padded); // big-endian -> little-endian
            return BitConverter.ToInt64(padded, 0);
        }

        private double BytesToScaledInt(byte[] bytes, double min, double max)
        {
            ulong raw = 0;
            for (int i = 0; i < bytes.Length; i++) {
                raw = (raw << 8) | bytes[i];  // big-endian
            }
            double maxInt = Math.Pow(2, bytes.Length * 8) - 1;
            return min + (raw / maxInt) * (max - min);
        }

        private double BytesToDouble(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return double.NaN;

            byte[] padded = new byte[8];
            int srcLen = Math.Min(bytes.Length, 8);
            Buffer.BlockCopy(bytes, bytes.Length - srcLen, padded, 8 - srcLen, srcLen);

            Array.Reverse(padded); // big-endian to little-endian
            return BitConverter.ToDouble(padded, 0);
        }

        private double BytesToScaledDouble(byte[] bytes, double min, double max)
        {
            if (bytes == null || bytes.Length == 0)
                return double.NaN;

            ulong raw = 0;
            for (int i = 0; i < bytes.Length; i++) 
                raw = (raw << 8) | bytes[i]; // big endian

            double maxInt = Math.Pow(2, bytes.Length * 8) - 1;
            if (maxInt < 0) return double.NaN;    

            double scale = (max - min) / maxInt;
            return (raw * scale) + min;
        }

        private byte[] RemoveMISBPrefix(byte[] klvData)
        {
            if (klvData.Length > 20 &&
                klvData[0] == 0x06 &&
                klvData[1] == 0x0E &&
                klvData[2] == 0x2B &&
                klvData[3] == 0x34) {

                int offset = 16; // Skip the 16 bit universal key

                int lenByte = klvData[offset++]; // Length of BER (Basic ENcoding RUles)
                int valuelength;

                if ((lenByte & 0x80) == 0) {
                    valuelength = lenByte;
                } else {
                    int numBytes = lenByte & 0x7F;
                    valuelength = 0;
                    for (int i = 0; i < numBytes; i++) {
                        valuelength = (valuelength << 8) | klvData[offset++];
                    }
                }

                // Extract the actual local set payload
                int remaining = klvData.Length - offset;
                int copyLen = Mathf.Min(valuelength, remaining);

                byte[] inner = new byte[copyLen];
                Buffer.BlockCopy(klvData, offset, inner, 0, copyLen);

                return inner;
            }          
            return klvData; //If the data is not a MISB Universal Key   
        }

        private static long ReadSignedInt(byte[] bytes)
        {
            long value = 0;
            foreach (var b in bytes)
            {
                value = (value << 8) | b;
            }

            int bits = bytes.Length * 8;
            long signBit = 1L << (bits - 1);
            if ((value & signBit) != 0)
            {
                long mask = ~((1L << bits) - 1);
                value |= mask;
            }
            return value;
        }

        private static double ScaleSignedInt(byte[] bytes, double min, double max)
        {
            long raw = ReadSignedInt(bytes);
            double denom = (Math.Pow(2, bytes.Length * 8) - 1.0);
            double maxPos = Math.Pow(2, bytes.Length * 8 - 1) - 1.0;
            double minNeg = -Math.Pow(2, bytes.Length * 8 - 1);
            return (raw - minNeg) * (max - min) / (maxPos - minNeg) + min;
        }

        public void SyncData(AISData aisData, KLVFrameMetadata metadata)
        {
            Debug.Log($"SyncData called. aisData null? {aisData == null}, " +
              $"ships null? {aisData?.ships == null}, " +
              $"count={aisData?.ships?.Count ?? -1}");

            if (metadata == null)
            {
                Debug.LogWarning("No KLV metadata available, skipping overlay.");
                return;
            }
            
            if (metadata != null)
            {
                Debug.Log($"KLV Frame Center: lat={metadata.frameCenterLat}, lon={metadata.frameCenterLon}");
            }


            if (aisData == null || aisData.ships == null || aisData.ships.Count == 0)
            {
                Debug.LogWarning("No AIS data available.");
                return;
            }

            double[] aisLatLon = new double[aisData.ships.Count * 2];

            for (int i = 0; i < aisData.ships.Count; i++)
            {
                aisLatLon[i * 2]     = aisData.ships[i].lat;
                aisLatLon[i * 2 + 1] = aisData.ships[i].lon;
            }

            double pitchFromHorizon = metadata.elevation - 90.0;

            // Debug.Log("Calling ProjectAISWrapper...");
            // var screenPoints = CInterop.ProjectAISWrapper(
            //     metadata.sensorLat,
            //     metadata.sensorLon,
            //     metadata.sensorAlt,
            //     metadata.azimuth,
            //     pitchFromHorizon,
            //     metadata.roll,
            //     Screen.width,
            //     Screen.height,
            //     metadata.fovVertical,
            //     metadata.fovHorizontal,
            //     aisLatLon
            // );

            Debug.Log("Calling ProjectAISUsingFrameCenter...");
            var screenPoints = CInterop.ProjectAISUsingFrameCenter(
                metadata.sensorLat,
                metadata.sensorLon,
                metadata.sensorAlt,
                metadata.frameCenterLat,
                metadata.frameCenterLon,
                Screen.width,
                Screen.height,
                metadata.fovHorizontal,
                metadata.fovVertical,
                aisLatLon
            );

            Debug.Log(
                $"[ProjectAIS] " +
                $"sensor=({metadata.sensorLat:F6},{metadata.sensorLon:F6}) | " +
                $"frameCenter=({metadata.frameCenterLat:F6},{metadata.frameCenterLon:F6}) | " +
                $"FOV=({metadata.fovHorizontal:F3},{metadata.fovVertical:F3}) | " +
                $"Video=1920x1080 | Screen={Screen.width}x{Screen.height} | "
            );

            overlaySystem.OverlayShips(aisData.ships, screenPoints);

            if (MetricsManager.Instance != null) {
                Vector2[] pts = new Vector2[screenPoints.Length];
                for (int i = 0; i < screenPoints.Length; i++) {
                    pts[i] = new Vector2(screenPoints[i].x, screenPoints[i].y);
                }
                MetricsManager.Instance.OnOverlaysRendered(aisData.ships, pts);
            }
        }
    }
}

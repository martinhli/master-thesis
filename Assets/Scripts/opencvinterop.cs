using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class OpenCVInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point2D { public float x, y;}

    [DllImport("OpenCVPlugin", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ProjectPoints(
        float[] objectPoints, int pointCount,
        float[] rvec, float[] tvec,
        float[] cameraMatrix, float[] distCoeffs,
        [Out]  Point2D[] outputPoints
    );

    public static Point2D[] Project(float[] objectPoints, int pointCount,
                                    float[] rvec, float[] tvec,
                                    float[] cameraMatrix, float[] distCoeffs)
    {
        Point2D[] output = new Point2D[pointCount];
        int result = ProjectPoints(objectPoints, pointCount, rvec, tvec, cameraMatrix, distCoeffs, output);
        if (result != 0) Debug.LogError("OpenCV ProjectPoints failed.");
        return output;
    }

    [DllImport("OpenCVPlugin", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ProjectAIS(
        double camLat, double camLon, double camAlt,
        double azimuth, double elevation, double roll,
        int imgWidth, int imgHeight,
        double fovV, double fovH,
        double[] aisLatLon, int pointCount,
        Point2D[] outputPoints
    );

    public static Point2D[] ProjectAISWrapper(
        double camLat, double camLon, double camAlt,
        double azimuth, double elevation, double roll,
        int imgWidth, int imgHeight,
        double fovV, double fovH,
        double[] aisLatLon
    ) {
        Point2D[] output = new Point2D[aisLatLon.Length / 2];
        int result = ProjectAIS(camLat, camLon, camAlt, azimuth, elevation, roll,
                                imgWidth, imgHeight, fovV, fovH,
                                aisLatLon, output.Length, output);
        if (result != 0) Debug.LogError("ProjectAIS failed.");
        if (output.Length > 0) Debug.Log($"ProjectAIS OUTPUT (first point): x={output[0].x}, y={output[0].y}");
        return output;
    }
}
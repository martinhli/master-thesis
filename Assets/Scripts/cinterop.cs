using System;
using UnityEngine;

public static class CInterop
{
    [Serializable]
    public struct Point2D
    {
        public float x, y;
        public Point2D(float x, float y) {this.x = x; this.y = y; }
    }

    /// C# version of the OpenCV projection
    /// Converts the AIS coordinates into screen-space points using ENU + pinhole conversion

    public static Point2D[] ProjectAISWrapper(
        double camLat, double camLon, double camAlt,
        double azimuthDeg, double elevationDeg, double rollDeg,
        int imgWidth, int imgHeight,
        double fovHdeg, double fovVdeg,
        double[] aisLatLon 
    )
    {
        if (aisLatLon == null || aisLatLon.Length < 2)
            return Array.Empty<Point2D>();

        int shipCount = aisLatLon.Length / 2;
        Point2D[] result = new Point2D[shipCount];

        // --- Constants ---
        const double R_EARTH = 6371000.0; // radius of the earth in meters
        double lat0 = camLat * Math.PI / 180.0;
        double cosLat0 = Math.Cos(lat0);

        double yawRad       = (90.0 - azimuthDeg) * Math.PI / 180.0; // bearing from North → yaw from X
        double pitchRad     = elevationDeg * Math.PI / 180.0;
        double rollRad      = rollDeg * Math.PI / 180.0;
        double tanHalfFovH  = Math.Tan(0.5 * fovHdeg * Math.PI / 180.0);
        double tanHalfFovV  = Math.Tan(0.5 * fovVdeg * Math.PI / 180.0);

        // Precompute rotation for yaw/pitch/roll
        double cy = Math.Cos(yawRad), sy = Math.Sin(yawRad);
        double cp = Math.Cos(pitchRad), sp = Math.Sin(pitchRad);
        double cr = Math.Cos(rollRad), sr = Math.Sin(rollRad);

        // World coordinates (ENU) -> Camera rotation matrix R
        double r00 = cy * cp;
        double r01 = cy * sp * sr - sy * cr;
        double r02 = cy * sp * cr + sy * sr;

        double r10 = sy * cp;
        double r11 = sy * sp * sr + cy * cr;
        double r12 = sy * sp * cr - cy * sr;

        double r20 = -sp;
        double r21 = cp * sr;
        double r22 = cp * cr;

        Debug.Log(
        $"ProjectAISWrapper: camLat={camLat}, camLon={camLon}, camAlt={camAlt}, " +
        $"az={azimuthDeg}, el={elevationDeg}, roll={rollDeg}, " +
        $"fovH={fovHdeg}, fovV={fovVdeg}, shipCount={shipCount}, " );

        for (int i = 0; i < shipCount; i++)
        {

            double shipLat = aisLatLon[2 * i + 0];
            double shipLon = aisLatLon[2 * i + 1];

            double latRad = shipLat * Math.PI / 180.0;
            double lonRad = shipLon * Math.PI / 180.0;

            double dLat = latRad - lat0;
            double dLon = (shipLon - camLon) * Math.PI / 180.0;

            double dNorth = dLat * R_EARTH;
            double dEast = dLon * R_EARTH * cosLat0;
            double dUp = 0.0 - camAlt; // vector from sea level to plane altitude

            /// ENU vector: v_enu
            double x_east = dEast;
            double y_north = dNorth;
            double z_up = dUp;

            /// Camera vector: v_cam = R * v_enu
            double x_cam = r00 * x_east + r01 * y_north + r02 * z_up; // camera right
            double y_cam = r10 * x_east + r11 * y_north + r12 * z_up; // camera up
            double z_cam = r20 * x_east + r21 * y_north + r22 * z_up; // camera north

            // only consider points in front of the camera
            // if (z_cam < 0){
            //   result[i] = new Point2D(-10000f, -10000f); //put point off screen
            //   continue;
            // }

            // Pinhole projection
            double x_norm = x_cam/(z_cam*tanHalfFovH); //normalizing the image coordinates
            double y_norm = y_cam/(z_cam*tanHalfFovV);

            if (i == 0)
            {
                Debug.Log(
                    $"Ship0: lat={shipLat}, lon={shipLon}, " +
                    $"dNorth={dNorth:F1}m, dEast={dEast:F1}m, " +
                    $"x_cam={x_cam:F2}, y_cam={y_cam:F2}, z_cam={z_cam:F2}, " +
                    $"x_norm={x_norm:F2}, y_norm={y_norm:F2}, " + 
                    $"tanHalfFovH={tanHalfFovH:F4}, tanHalfFovV={tanHalfFovV:F4}");
            }

            // Cull outside the FOV
            if (Math.Abs(x_norm) > 1.0 || Math.Abs(y_norm) > 1.0)
            {
                if (i == 0)
                {
                    Debug.Log("Ship0: culled by FOV (|x_norm| or |y_norm| > 1)");
                }
                result[i] = new Point2D(-10000f, 10000f);
                continue;
            }

            // Turn into screen coordinates (u,v)
            double u = (x_norm * 0.5 + 0.5) * imgWidth;
            double v = (1.0-(y_norm * 0.5 + 0.5)) * imgHeight;

            result[i] = new Point2D((float)u, (float)v);


        }

        return result;
    }

        public static Point2D[] ProjectAISUsingFrameCenter(
            double sensorLat, double sensorLon, double sensorAlt,
            double frameCenterLat, double frameCenterLon,
            int imgWidth, int imgHeight,
            double fovHdeg, double fovVdeg,
            double[] aisLatLon
        )
    {
        if (aisLatLon == null || aisLatLon.Length < 2)
            return Array.Empty<Point2D>();

        int shipCount = aisLatLon.Length / 2;
        Point2D[] result = new Point2D[shipCount];

        const double R_EARTH = 6371000.0;
        const double Deg2Rad = Math.PI / 180.0;

        double lat0 = sensorLat * Deg2Rad;
        double cosLat0 = Math.Cos(lat0);

        // --- Build ENU vector for the frame center (sensor -> frame center) ---
        double fcLatRad = frameCenterLat * Deg2Rad;
        double dLatC = fcLatRad - lat0;
        double dLonC = (frameCenterLon - sensorLon) * Deg2Rad;

        double dNorthC = dLatC * R_EARTH;
        double dEastC  = dLonC * R_EARTH * cosLat0;
        double dUpC    = 0.0 - sensorAlt;   // assume frame center at sea level

        // Forward = direction to frame center in ENU
        double fx = dEastC;
        double fy = dNorthC;
        double fz = dUpC;
        Normalize(ref fx, ref fy, ref fz);

        // World up in ENU coordinates
        double upWx = 0.0, upWy = 0.0, upWz = 1.0;

        // Right = upW × forward
        Cross(upWx, upWy, upWz, fx, fy, fz, out double rx, out double ry, out double rz);
        Normalize(ref rx, ref ry, ref rz);

        // Camera up = forward × right
        Cross(fx, fy, fz, rx, ry, rz, out double ux, out double uy, out double uz);
        Normalize(ref ux, ref uy, ref uz);

        // Rotation matrix ENU -> camera
        // cameraX = right ⋅ v_enu
        // cameraY = up    ⋅ v_enu
        // cameraZ = fwd   ⋅ v_enu
        double r00 = rx, r01 = ry, r02 = rz;
        double r10 = ux, r11 = uy, r12 = uz;
        double r20 = fx, r21 = fy, r22 = fz;

        double tanHalfFovH = Math.Tan(0.5 * fovHdeg * Deg2Rad);
        double tanHalfFovV = Math.Tan(0.5 * fovVdeg * Deg2Rad);

        Debug.Log(
            $"ProjectAISUsingFrameCenter: sensorLat={sensorLat}, sensorLon={sensorLon}, " +
            $"frameCenterLat={frameCenterLat}, frameCenterLon={frameCenterLon}, " +
            $"fovH={fovHdeg}, fovV={fovVdeg}, shipCount={shipCount}");

        for (int i = 0; i < shipCount; i++)
        {
            double shipLat = aisLatLon[2 * i + 0];
            double shipLon = aisLatLon[2 * i + 1];

            double shipLatRad = shipLat * Deg2Rad;
            double dLat = shipLatRad - lat0;
            double dLon = (shipLon - sensorLon) * Deg2Rad;

            double dNorth = dLat * R_EARTH;
            double dEast  = dLon * R_EARTH * cosLat0;
            double dUp    = 0.0 - sensorAlt;

            double x_east  = dEast;
            double y_north = dNorth;
            double z_up    = dUp;

            // ENU -> camera
            double x_cam = r00 * x_east + r01 * y_north + r02 * z_up; // right
            double y_cam = r10 * x_east + r11 * y_north + r12 * z_up; // up
            double z_cam = r20 * x_east + r21 * y_north + r22 * z_up; // forward

            if (i == 0)
            {
                Debug.Log(
                    $"Ship0 (frame-center-based): lat={shipLat}, lon={shipLon}, " +
                    $"dNorth={dNorth:F1}m, dEast={dEast:F1}m, " +
                    $"x_cam={x_cam:F2}, y_cam={y_cam:F2}, z_cam={z_cam:F2}");
            }

            // Expect z_cam > 0 for points in front of camera
            if (z_cam <= 0.0)
            {
                result[i] = new Point2D(-10000f, -10000f);
                continue;
            }

            double x_norm = x_cam / (z_cam * tanHalfFovH);
            double y_norm = y_cam / (z_cam * tanHalfFovV);

            if (i == 0)
            {
                Debug.Log($"Ship0 norm: x_norm={x_norm:F2}, y_norm={y_norm:F2}");
            }

            // Cull outside the FOV
            if (Math.Abs(x_norm) > 1.0 || Math.Abs(y_norm) > 1.0)
            {
                if (i == 0)
                    Debug.Log("Ship0 culled by FOV in ProjectAISUsingFrameCenter.");
                result[i] = new Point2D(-10000f, -10000f);
                continue;
            }

            double u = (x_norm * 0.5 + 0.5) * imgWidth;
            double v = (1.0 - (y_norm * 0.5 + 0.5)) * imgHeight;

            result[i] = new Point2D((float)u, (float)v);
        }

        return result;
    }


    static void Normalize(ref double x, ref double y, ref double z)
    {
        double mag = Math.Sqrt(x * x + y * y + z * z);
        if (mag < 1e-9) { x = 0; y = 0; z = 1; return; }
        x /= mag; y /= mag; z /= mag;
    }

    static void Cross(double ax, double ay, double az,
                    double bx, double by, double bz,
                    out double cx, out double cy, out double cz)
    {
        cx = ay * bz - az * by;
        cy = az * bx - ax * bz;
        cz = ax * by - ay * bx;
    }
}
using System;
using UnityEngine;

namespace Data
{
    public class KalmanFilter
    {
        // State vector [x, vx, z, vz]
        private Vector4 state;

        // State covariance matrix
        private float[,] P;

        // Process noise covariance matrix
        private float[,] Q;

        // Measurement noise covariance matrix
        private float[,] R;

        // State transition matrix
        private float[,] F;

        // Measurement matrix
        private float[,] H;

        // Time step (delta time)
        private float dt;

        // White acceleration noise intensity for the constant-velocity model.
        private const float ProcessNoiseAccelerationVariance = 1f;

        public KalmanFilter(float initialX, float initialZ, float initialVx = 0f, float initialVz = 0f)
        {
            state = new Vector4(initialX, initialVx, initialZ, initialVz);

            P = new float[4, 4]
            {
                { 100, 0, 0, 0 },
                { 0, 10, 0, 0 },
                { 0, 0, 100, 0 },
                { 0, 0, 0, 10 }
            };

            float processNoise = 0.1f;
            Q = new float[4, 4]
            {
                { processNoise, 0, 0, 0 },
                { 0, processNoise, 0, 0 },
                { 0, 0, processNoise, 0 },
                { 0, 0, 0, processNoise }
            };

            float measurementNoise = 25f;
            R = new float[2, 2]
            {
                { measurementNoise, 0 },
                { 0, measurementNoise }
            };

            H = new float[2, 4]
            {
                { 1, 0, 0, 0 },
                { 0, 0, 1, 0 }
            };

            dt = 1.0f;
        }

        public void Predict(float deltaTime)
        {
            dt = Mathf.Max(deltaTime, 0.0001f);

            F = new float[4, 4]
            {
                { 1, dt, 0, 0 },
                { 0, 1, 0, 0 },
                { 0, 0, 1, dt },
                { 0, 0, 0, 1 }
            };

            float dt2 = dt * dt;
            float dt3 = dt2 * dt;
            float dt4 = dt2 * dt2;
            float q = ProcessNoiseAccelerationVariance;
            Q = new float[4, 4]
            {
                { 0.25f * dt4 * q, 0.5f * dt3 * q, 0, 0 },
                { 0.5f * dt3 * q, dt2 * q, 0, 0 },
                { 0, 0, 0.25f * dt4 * q, 0.5f * dt3 * q },
                { 0, 0, 0.5f * dt3 * q, dt2 * q }
            };

            state = new Vector4(
                state.x + state.y * dt,
                state.y,
                state.z + state.w * dt,
                state.w
            );

            float[,] fTranspose = Transpose(F);
            P = MatrixAdd(MatrixMultiply(MatrixMultiply(F, P), fTranspose), Q);
        }

        public void Update(Vector3 measurement, SensorType sensorType)
        {
            SetMeasurementNoise(sensorType);

            float[] z = new float[2] { measurement.x, measurement.z };
            float[] zPred = new float[2] { state.x, state.z };
            float[] y = new float[2] { z[0] - zPred[0], z[1] - zPred[1] };

            float[,] hTranspose = Transpose(H);
            float[,] s = MatrixAdd(MatrixMultiply(MatrixMultiply(H, P), hTranspose), R);
            float[,] sInverse = Inverse(s);
            float[,] k = MatrixMultiply(MatrixMultiply(P, hTranspose), sInverse);

            state.x += k[0, 0] * y[0] + k[0, 1] * y[1];
            state.y += k[1, 0] * y[0] + k[1, 1] * y[1];
            state.z += k[2, 0] * y[0] + k[2, 1] * y[1];
            state.w += k[3, 0] * y[0] + k[3, 1] * y[1];

            float[,] i = new float[4, 4]
            {
                { 1, 0, 0, 0 },
                { 0, 1, 0, 0 },
                { 0, 0, 1, 0 },
                { 0, 0, 0, 1 }
            };

            float[,] kh = MatrixMultiply(k, H);
            float[,] iMinusKh = MatrixSubtract(i, kh);
            float[,] iMinusKhTranspose = Transpose(iMinusKh);
            float[,] p1 = MatrixMultiply(MatrixMultiply(iMinusKh, P), iMinusKhTranspose);
            float[,] kTranspose = Transpose(k);
            float[,] p2 = MatrixMultiply(MatrixMultiply(k, R), kTranspose);
            P = MatrixAdd(p1, p2);
        }

        private void SetMeasurementNoise(SensorType sensorType)
        {
            float noiseVariance;
            if (sensorType == SensorType.AIS)
            {
                noiseVariance = 10 * 10;
            }
            else if (sensorType == SensorType.Radar)
            {
                noiseVariance = 50 * 50;
            }
            else if (sensorType == SensorType.EOIR)
            {
                noiseVariance = 20 * 20;
            }
            else
            {
                noiseVariance = 25 * 25;
            }

            R[0, 0] = noiseVariance;
            R[1, 1] = noiseVariance;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(state.x, 0, state.z);
        }

        public Vector3 GetVelocity()
        {
            return new Vector3(state.y, 0, state.w);
        }

        public float GetPositionUncertainty()
        {
            return Mathf.Sqrt((P[0, 0] + P[2, 2]) / 2f);
        }

        private float[,] Transpose(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            float[,] transpose = new float[cols, rows];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    transpose[j, i] = matrix[i, j];
                }
            }

            return transpose;
        }

        private float[,] Inverse(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            if (rows != cols)
            {
                throw new ArgumentException("Matrix must be square to compute inverse.");
            }

            float a = matrix[0, 0];
            float b = matrix[0, 1];
            float c = matrix[1, 0];
            float d = matrix[1, 1];

            float det = a * d - b * c;
            if (Mathf.Abs(det) < 0.0001f)
            {
                const float eps = 0.0001f;
                float[,] inv = new float[2, 2]
                {
                    { 1, 0 },
                    { 0, 1 }
                };
                inv[0, 0] += eps;
                inv[1, 1] += eps;
                return inv;
            }

            float[,] inverse = new float[2, 2];
            inverse[0, 0] = d / det;
            inverse[0, 1] = -b / det;
            inverse[1, 0] = -c / det;
            inverse[1, 1] = a / det;
            return inverse;
        }

        private float[,] MatrixMultiply(float[,] A, float[,] B)
        {
            int rowsA = A.GetLength(0);
            int colsA = A.GetLength(1);
            int rowsB = B.GetLength(0);
            int colsB = B.GetLength(1);

            float[,] result = new float[rowsA, colsB];
            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    result[i, j] = 0;
                    for (int k = 0; k < colsA; k++)
                    {
                        result[i, j] += A[i, k] * B[k, j];
                    }
                }
            }
            return result;
        }

        private float[,] MatrixAdd(float[,] A, float[,] B)
        {
            int rows = A.GetLength(0);
            int cols = A.GetLength(1);
            float[,] result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = A[i, j] + B[i, j];
                }
            }
            return result;
        }

        private float[,] MatrixSubtract(float[,] A, float[,] B)
        {
            int rows = A.GetLength(0);
            int cols = A.GetLength(1);
            float[,] result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = A[i, j] - B[i, j];
                }
            }
            return result;
        }
    }
}

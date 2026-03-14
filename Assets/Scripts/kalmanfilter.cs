using System;
using UnityEngine;

namespace Data
{
    public class KalmanFilter
    {
        // State vector [x, vx, z, vz]
        private Vector4 state;

        // State covariance matrix (4x4, flattened to 16 elements)
        private float[,] P;

        // Process noise covariance matrix (4x4, flattened to 16 elements)
        private float[,] Q;

        // Measurement noise covariance matrix (2x2, flattened to 4 elements)
        private float[,] R;

        // State transition matrix (4x4, flattened to 16 elements)
        private float[,] F;

        // Measurement matrix (2x4, flattened to 8 elements)
        private float[,] H;

        // Time step (delta time)
        private float dt;

        // White acceleration noise intensity for the constant-velocity model.
        private const float ProcessNoiseAccelerationVariance = 1f;

        public KalmanFilter(float initialX, float initialZ, float initialVx = 0f, float initialVz = 0f)
        {
            // Initialize state vector
            state = new Vector4(initialX, initialVx, initialZ, initialVz);

            // Initialize state covariance matrix P
            P = new float[4, 4]
            {
                { 100, 0, 0, 0 },
                { 0, 10, 0, 0 },
                { 0, 0, 100, 0 },
                { 0, 0, 0, 10}
            };

            // Process noise (assumes some uncertainty in the model)
            float processnoise = 0.1f;

            // Initialize process noise covariance matrix Q
            Q = new float[4, 4]
            {
                { processnoise, 0, 0, 0 },
                { 0, processnoise, 0, 0 },
                { 0, 0, processnoise, 0 },
                { 0, 0, 0, processnoise }
            };

            // Measurement noise (assumes some uncertainty in the sensors)
            float measurementnoise = 25f; // By default, there is 5 units of noise in the measurement, so variance is 25

            // Initialize measurement noise covariance matrix R
            R = new float[2, 2]
            {
                { measurementnoise, 0 },
                { 0, measurementnoise }
            };

            // Initialize measurement matrix H (we only measure position, not velocity)
            H = new float[2, 4]
            {
                {1, 0, 0, 0}, // x measurement
                {0, 0, 1, 0} // z measurement
            };

            // Initialize time step (will be updated in the Predict function)
            dt = 1.0f; // Assuming 1 second between measurements for simplicity
        }

        /// <summary>
        /// Predict: Extrapolate state and covariance forward in time based on the model
        /// </summary>

        public void Predict(float deltaTime)
        {
            // Update the time step
            dt = Mathf.Max(deltaTime, 0.0001f);

            // Update state transition matrix F based on the time step
            F = new float[4, 4]
            {
                { 1, dt, 0, 0 },
                { 0, 1, 0, 0 },
                { 0, 0, 1, dt },
                { 0, 0, 0, 1 }
            };

            // Build process noise Q from dt for a constant-velocity model with white acceleration input.
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

            // Predict the next state: state = F * state
            Vector4 predictedState = new Vector4(
                state.x + state.y * dt, // x + vx*dt
                state.y, // vx remains the same
                state.z + state.w * dt, // z + vz*dt
                state.w // vz remains the same
            );
            state = predictedState;

            // Predict the next state covariance: P = F * P * F^T + Q
            // Need to add a function to compute the transpose of F
            float[,] F_transpose = Transpose(F);
            // Need to add matrix multiplication and addition functions to compute P
            P = MatrixAdd(MatrixMultiply(MatrixMultiply(F, P), F_transpose), Q);
        }

        /// <summary>
        /// Update: Incorporate a new measurement to correct the state and covariance
        /// </summary>
        
        public void Update(Vector3 measurement, SensorType sensorType)
        {
            // Set the measurement noise based on sensor type
            // Need to implement a function to set R based on sensor type
            SetMeasurementNoise(sensorType);

            // Measurement vector z (only x and z)
            float[] z = new float[2] { measurement.x, measurement.z };

            // Predicted measurement z_pred: measurement z = H * state x
            float[] z_pred = new float[2];
            z_pred[0] = state.x;
            z_pred[1] = state.z;

            // Residual of the measurement (measurement - predicted measurement): y = z - z_pred
            float[] y = new float[2] { z[0] - z_pred[0], z[1] - z_pred[1]};

            // Residual covariance: S = H * P * H^T + R
            // Need to add a function to add 2x2 matrixes and to compute the transpose of H
            float[,] H_transpose = Transpose(H);
            float[,] S = MatrixAdd(MatrixMultiply(MatrixMultiply(H, P), H_transpose), R);

            // Kalman gain: K = P * H^T * S^-1
            // Need to add a function to compute the inverse of S
            float[,] S_inverse = Inverse(S);
            float[,] K = MatrixMultiply(MatrixMultiply(P, H_transpose), S_inverse);

            // Update state estimate: state x = state x + K * y
            state.x += K[0, 0] * y[0] + K[0,1] * y[1];
            state.y += K[1, 0] * y[0] + K[1, 1] * y[1];
            state.z += K[2, 0] * y[0] + K[2, 1] * y[1];
            state.w += K[3, 0] * y[0] + K[3, 1] * y[1];

            // Update state covariance: P = (I - K * H) * P
            float[,] I = new float[4, 4]
            {
                { 1, 0, 0, 0 },
                { 0, 1, 0, 0 },
                { 0, 0, 1, 0 },
                { 0, 0, 0, 1 }
            }; // Identity matrix
            float[,] KH = MatrixMultiply(K, H);
            float[,] I_minus_KH = MatrixSubtract(I, KH);
            float[,] I_minus_KH_transpose = Transpose(I_minus_KH);
            float[,] P1 = MatrixMultiply(MatrixMultiply(I_minus_KH, P), I_minus_KH_transpose);
            float[,] K_transpose = Transpose(K);
            float[,] P2 = MatrixMultiply(MatrixMultiply(K, R), K_transpose);
            P = MatrixAdd(P1, P2); // Joseph form for numerical stability
        }

        /// <summary>
        /// Helper Functions
        /// </summary>

        private void SetMeasurementNoise(SensorType sensorType)
        {
            float noisevariance;

            if (sensorType == SensorType.AIS)
            {
                noisevariance = 10*10; // AIS has 10 units of noise, so variance is 100
            }
            else if (sensorType == SensorType.Radar)
            {
                noisevariance = 50*50; // Radar has 50 units of noise, so variance is 2500
            }
            else if (sensorType == SensorType.EOIR)
            {
                noisevariance = 20*20; // EOIR has 20 units of noise, so variance is 400
            }
            else 
            {
                noisevariance = 25*25; // Default to 25 units of noise, so variance is 625
            }

            R[0, 0] = noisevariance;
            R[1, 1] = noisevariance;
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
            return Mathf.Sqrt((P[0, 0] + P[2, 2]) / 2f); // Average of x and z position uncertainty
        }

        /// <summary>
        /// Matrix Operations
        /// </summary>
        
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

            // For 2x2 matrix, we can compute the inverse directly
            float a = matrix[0, 0];
            float b = matrix[0, 1];
            float c = matrix[1, 0];
            float d = matrix[1, 1];

            float det = a*d - b*c;

            if (Mathf.Abs(det) < 0.0001f)
            {
                const float eps = 0.0001f; // Add a small value to the diagonal to make it invertible
        
                float[,] inv = new float[2, 2] {
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
            int rowsA = A.GetLength(0);
            int colsA = A.GetLength(1);

            float[,] result = new float[rowsA, colsA];

            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsA; j++)
                {
                    result[i,j] = A[i, j] + B[i, j];
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
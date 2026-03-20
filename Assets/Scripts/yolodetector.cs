using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;




public class yolodetector : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset yoloModel; // Reference to the YOLO .sentis model
    public float confidenceThreshold = 0.5f;
    private Unity.InferenceEngine.Worker worker;
    private Model runtimeModel;

    [System.Serializable]
    public class Detection
    {
        public Rect boundingBox; // x, y, width, height
        public float confidence; // 0.0 to 1.0
        public string className; // "ship"
    }

    void Start()
    {
        // Define a function to load the model and create a worker
        LoadModel();
    }

    void LoadModel()
    {
        if (yoloModel == null)
        {
            Debug.LogError("[YOLO] No model assigned!");
            return;
        }

        // Load the model from the asset
        runtimeModel = Unity.InferenceEngine.ModelLoader.Load(yoloModel);

        // Create a worker to run the model
        worker = CreateBestWorker(runtimeModel);

        Debug.Log("[YOLO] Model loaded and worker created.");
    }

   Unity.InferenceEngine.Worker CreateBestWorker(Model model)
   {
        try
        {
            // Try to create a worker on the GPU first
            return new Unity.InferenceEngine.Worker(model, BackendType.GPUCompute);
        }
        catch
        {
            // Fallback to CPU if GPU is not available
            return new Unity.InferenceEngine.Worker(model, BackendType.CPU);
        }
   }

    public List<Detection> DetectShips(Texture2D inputImage)
    {
        if (worker == null)
        {
            Debug.LogError("[YOLO] Worker not initialized!");
            return new List<Detection>();
        }

        // Preprocess the input image (resize, normalize, etc.)
        Tensor<float> inputTensor = PreprocessImage(inputImage);

        // Run inference
        worker.Schedule(inputTensor);

        // Get the output tensor (assuming the model outputs a tensor with detections)
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        if (outputTensor == null)
        {
            inputTensor.Dispose();
            Debug.LogError("[YOLO] Unexpected output type. Expected Tensor<float>.");
            return new List<Detection>();
        }

        // Output can live on GPU; read back to CPU before indexing.
        Tensor<float> cpuOutputTensor = outputTensor.ReadbackAndClone();

        // Postprocess the output to extract detections
        List<Detection> detections = PostProcessOutput(cpuOutputTensor);

        // Dispose of tensors to free memory
        inputTensor.Dispose();
        cpuOutputTensor.Dispose();

        Debug.Log($"[YOLO] Detected {detections.Count} ships.");
        return detections;
    }

    public bool TryGetBestShipDetection(Texture2D inputImage, out Detection bestDetection)
    {
        bestDetection = null;

        List<Detection> detections = DetectShips(inputImage);
        if (detections == null || detections.Count == 0)
        {
            return false;
        }

        bestDetection = detections.OrderByDescending(d => d.confidence).FirstOrDefault();
        return bestDetection != null;
    }

    Tensor<float> PreprocessImage(Texture2D inputimage)
    {
        // Resize to the model's expected input size
        Texture2D resizedImage = ResizeTexture(inputimage, 640, 640);

        // Convert to tensor (normalize pixel values to [0,1]  with RGB number of channels)
        Tensor<float> inputTensor = Unity.InferenceEngine.TextureConverter.ToTensor(resizedImage, 0, 1, 3);

        // Clean up the resized image
        Destroy(resizedImage);

        return inputTensor;
    }

    Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(source, rt); // Copy the source texture to the render texture

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        // Create a new Texture2D and read the pixels from the render texture
        Texture2D result = new Texture2D(newWidth, newHeight);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        // Set the active render texture back to the previous one and release the temporary render texture
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    List<Detection> PostProcessOutput(Tensor<float> outputTensor)
    {
        List<Detection> detections = new List<Detection>();

        // YOLO typically outputs a tensor with shape [num_detections, 6] where each detection has:
        // Each detection has [x_center, y_center, width, height, confidence, class_id]

        int numDetections = outputTensor.shape.rank >= 3 ? outputTensor.shape[1] : outputTensor.shape[0];

        for (int i = 0; i < numDetections; i++)
        {
            float confidence = outputTensor.shape.rank >= 3 ? outputTensor[0, i, 4] : outputTensor[i, 4];

            if (confidence >= confidenceThreshold)
            {
                float xCenter = outputTensor.shape.rank >= 3 ? outputTensor[0, i, 0] : outputTensor[i, 0];
                float yCenter = outputTensor.shape.rank >= 3 ? outputTensor[0, i, 1] : outputTensor[i, 1];
                float width = outputTensor.shape.rank >= 3 ? outputTensor[0, i, 2] : outputTensor[i, 2];
                float height = outputTensor.shape.rank >= 3 ? outputTensor[0, i, 3] : outputTensor[i, 3];

                Detection detection = new Detection
                {
                    boundingBox = new Rect(xCenter - width / 2, yCenter - height / 2, width, height),
                    confidence = confidence,
                    className = "ship" // Model is trained to detect only ships
                };
                detections.Add(detection);
            }
        }
        return detections;
    }

    void OnDestroy()
    {
        // Dispose of the worker when the script is destroyed
        if (worker != null)
        {
            worker.Dispose();
            Debug.Log("[YOLO] Worker disposed.");
        }
    }

}
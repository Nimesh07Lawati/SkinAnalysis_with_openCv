using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using System.Threading;
using System.Threading.Tasks;
using Size = OpenCvSharp.Size;

namespace SkinCareAiIntegration.AndroidAiSetUp;

public partial class MainView : ContentPage
{
    private VideoCapture _capture;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isCapturing = false;
    private DateTime _lastFrameUpdate = DateTime.MinValue;
    private readonly object _locker = new object();
    private Mat _latestCapturedFrame;
    private InferenceSession _onnxSession;

    public MainView()
    {
        InitializeComponent();
        LoadOnnxModel();
    }

    private void LoadOnnxModel()
    {
        try
        {
            string modelPath = GetModelPath();
            Console.WriteLine($"Looking for model at: {modelPath}"); // Debug path

            if (!File.Exists(modelPath))
            {
                Console.WriteLine("Model file not found at specified path!"); // Debug
                throw new FileNotFoundException("Model file not found");
            }
            else
            {
                Console.WriteLine("Model file found!"); // Debug
            }

            var options = new SessionOptions();
            _onnxSession = new InferenceSession(modelPath, options);

            // Additional verification
            if (_onnxSession == null)
            {
                Console.WriteLine("InferenceSession creation failed!"); // Debug
                throw new Exception("Failed to create inference session");
            }

            var inputMeta = _onnxSession.InputMetadata.First();
            Console.WriteLine($"Model loaded successfully. Input: {inputMeta.Key}, Shape: {string.Join(",", inputMeta.Value.Dimensions)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LOAD MODEL ERROR: {ex}"); // Detailed error
            MainThread.BeginInvokeOnMainThread(async () =>
                await DisplayAlert("Error", $"Failed to load model: {ex.Message}", "OK"));
            _onnxSession = null;
        }
    }

    private string GetModelPath()
    {
#if ANDROID
        var modelPath = Path.Combine(FileSystem.AppDataDirectory, "skin_analysis.onnx");
        if (!File.Exists(modelPath))
        {
            using var assetStream = Android.App.Application.Context.Assets.Open("skin_analysis.onnx");
            using var fileStream = File.Create(modelPath);
            assetStream.CopyTo(fileStream);
        }
        return modelPath;
#else
        return Path.Combine(AppContext.BaseDirectory, "skin_analysis.onnx");
#endif
    }

    private async void OnStartCameraClicked(object sender, EventArgs e)
    {
        if (!_isCapturing)
        {
            await StartCameraAsync();
            CameraButton.Text = "Stop Camera";
        }
        else
        {
            StopCamera();
            CameraButton.Text = "Start Camera";
        }
    }

    private async Task StartCameraAsync()
    {
        try
        {
            lock (_locker)
            {
                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                    throw new Exception("Could not open camera");

                _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                _capture.Set(VideoCaptureProperties.FrameHeight, 480);

                _isCapturing = true;
                _cancellationTokenSource = new CancellationTokenSource();

                Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Camera error: {ex.Message}", "OK");
        }
    }

    private async Task CaptureLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isCapturing)
        {
            try
            {
                using var frame = new Mat();
                if (_capture == null || !_capture.Read(frame) || frame.Empty())
                    continue;

                lock (_locker)
                {
                    _latestCapturedFrame?.Dispose();
                    _latestCapturedFrame = frame.Clone();
                }

                UpdatePreview(frame);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frame error: {ex.Message}");
            }

            await Task.Delay(30); // ~30 FPS
        }
    }

    private void UpdatePreview(Mat frame)
    {
        if ((DateTime.Now - _lastFrameUpdate).TotalMilliseconds < 100)
            return;

        _lastFrameUpdate = DateTime.Now;

        try
        {
            byte[] imageData;
            using (var frameCopy = frame.Clone())
            {
                imageData = frameCopy.ToBytes(".png");
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(imageData));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Preview Update Error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Frame Conversion Error: {ex.Message}");
        }
    }

    private void StopCamera()
    {
        lock (_locker)
        {
            _isCapturing = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopCamera();
    }

    private float[] PreprocessImage(Mat mat, int width, int height)
    {
        // Convert to RGB if needed (OpenCV uses BGR by default)
        Mat rgb = new Mat();
        if (mat.Channels() == 1)
            Cv2.CvtColor(mat, rgb, ColorConversionCodes.GRAY2RGB);
        else
            Cv2.CvtColor(mat, rgb, ColorConversionCodes.BGR2RGB);

        // Resize and normalize
        Mat resized = new Mat();
        Cv2.Resize(rgb, resized, new Size(width, height));
        resized.ConvertTo(resized, MatType.CV_32FC3, 1.0 / 255);

        // Convert to channel-first array
        var input = new float[1 * 3 * height * width];
        int index = 0;

        // Extract channels (R, G, B)
        var channels = resized.Split();
        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    input[index++] = channels[c].At<float>(y, x);
                }
            }
            channels[c].Dispose();
        }

        return input;
    }

    private string RunSkinAnalysis(Mat mat)
    {
        try
        {
            if (_onnxSession == null)
                throw new InvalidOperationException("ONNX session not initialized");

            // Get model input metadata
            var inputMeta = _onnxSession.InputMetadata.First();
            var inputName = inputMeta.Key;
            var inputShape = inputMeta.Value.Dimensions;

            // Preprocess and create tensor
            var inputTensor = new DenseTensor<float>(PreprocessImage(mat, inputShape[2], inputShape[3]), inputShape);

            // Create inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            // Run inference
            using var results = _onnxSession.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();

            // Process results
            var labels = new[] { "Clear", "Acne", "Scarring", "Other" };
            int predictedIndex = Array.IndexOf(output, output.Max());
            return labels[predictedIndex];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Analysis error: {ex}");
            return $"Error: {ex.Message}";
        }
    }

    private async void OnAnalyzeSkinClicked(object sender, EventArgs e)
    {
        if (_onnxSession == null)
        {
            await DisplayAlert("Error", "Model not loaded", "OK");
            return;
        }

        Mat frameCopy;
        lock (_locker)
        {
            if (_latestCapturedFrame == null || _latestCapturedFrame.Empty())
            {
                DisplayAlert("Error", "No frame available", "OK");
                return;
            }
            frameCopy = _latestCapturedFrame.Clone();
        }

        try
        {
            SkinAnalysisLabel.Text = "Analyzing...";
            var result = await Task.Run(() => RunSkinAnalysis(frameCopy));
            SkinAnalysisLabel.Text = $"Result: {result}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Analysis failed: {ex.Message}", "OK");
        }
        finally
        {
            frameCopy.Dispose();
        }
    }
}
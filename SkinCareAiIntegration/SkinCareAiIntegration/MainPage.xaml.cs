using OpenCvSharp;
using OpenCvSharp.Extensions;
using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Size = OpenCvSharp.Size;

namespace SkinCareAiIntegration;

public partial class MainPage : ContentPage
{
    private VideoCapture _capture;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isCapturing = false;
    private DateTime _lastFrameUpdate = DateTime.MinValue;
    private CascadeClassifier _faceCascade;
    private readonly object _locker = new object();
    private Mat _latestCapturedFrame;

    public MainPage()
    {
        InitializeComponent();
        LoadCascadeClassifier();
    }

    private void LoadCascadeClassifier()
    {
        try
        {
            var cascadePath = Path.Combine(AppContext.BaseDirectory, "haarcascade_frontalface_default.xml");
            if (!File.Exists(cascadePath))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Error", "Face detection model file not found", "OK"));
                return;
            }

            _faceCascade = new CascadeClassifier(cascadePath);
            if (_faceCascade.Empty())
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Error", "Failed to load face detection model", "OK"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await DisplayAlert("Error", $"Failed to load classifier: {ex.Message}", "OK"));
        }
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
                _capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
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

                using var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                var faces = _faceCascade?.DetectMultiScale(
                    gray, 1.1, 5, HaarDetectionTypes.ScaleImage, new Size(30, 30));

                if (faces?.Length > 0)
                {
                    foreach (var face in faces)
                        Cv2.Rectangle(frame, face, Scalar.Red, 2);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FaceDetectionLabel.Text = faces?.Length > 0
                        ? $"{faces.Length} face(s) detected"
                        : "No face detected";
                });

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

    private async void OnCapturePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            lock (_locker)
            {
                if (_latestCapturedFrame == null || _latestCapturedFrame.Empty())
                {
                    DisplayAlert("Error", "No frame available to capture.", "OK");
                    return;
                }

                var fileName = $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                Cv2.ImWrite(filePath, _latestCapturedFrame);
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Saved", $"Photo saved to: {filePath}", "OK"));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to capture photo: {ex.Message}", "OK");
        }
    }

    private async void OnAnalyzeSkinClicked(object sender, EventArgs e)
    {
        try
        {
            lock (_locker)
            {
                if (_latestCapturedFrame == null || _latestCapturedFrame.Empty())
                {
                    DisplayAlert("Error", "No frame available for analysis.", "OK");
                    return;
                }

                var avgColor = Cv2.Mean(_latestCapturedFrame);

                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Skin Analysis",
                        $"Avg Skin Tone (BGR):\nB: {avgColor.Val0:F0}, G: {avgColor.Val1:F0}, R: {avgColor.Val2:F0}",
                        "OK"));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Skin analysis failed: {ex.Message}", "OK");
        }
    }
}

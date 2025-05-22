using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Runtime.InteropServices;
using System.Timers;
using Color = Microsoft.Maui.Graphics.Color;
using Timer = System.Timers.Timer;

namespace SkinCareAiIntegration.ForAndroid;

public partial class CameraLivePage : ContentPage
{
    private VideoCapture? _capture;
    private CascadeClassifier? _faceCascade;
    private Timer? _frameGrabber;
    private bool _isProcessing = false;

    public CameraLivePage()
    {
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            await InitializeCameraAsync();
        };
    }

    private async Task InitializeCameraAsync()
    {
        try
        {
            _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
            _capture = new VideoCapture(0);
            _capture.ImageGrabbed += ProcessFrame;

            _capture.Start();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void ProcessFrame(object? sender, EventArgs e)
    {
        if (_isProcessing || _capture == null) return;

        _isProcessing = true;

        using Mat frame = new();
        _capture.Retrieve(frame);

        if (frame.IsEmpty)
        {
            _isProcessing = false;
            return;
        }

        using Mat grayFrame = new();
        CvInvoke.CvtColor(frame, grayFrame, ColorConversion.Bgr2Gray);

        var faces = _faceCascade?.DetectMultiScale(grayFrame, 1.1, 5) ?? Array.Empty<System.Drawing.Rectangle>();

        foreach (var face in faces)
        {
            CvInvoke.Rectangle(frame, face, new MCvScalar(255, 0, 0), 2);
        }

        // Update UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FaceCountLabel.Text = $"Faces Detected: {faces.Length}";
            CameraImage.Source = ConvertMatToImageSource(frame);
        });

        _isProcessing = false;
    }

    private ImageSource ConvertMatToImageSource(Mat mat)
    {
        using var ms = mat.ToMemoryStream;
        return ImageSource.FromStream(() => new MemoryStream(ms.ToArray()));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _capture?.Stop();
        _capture?.Dispose();
    }
}

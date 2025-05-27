using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Diagnostics;
using System.Drawing;
using Size = System.Drawing.Size; // Resolve ambiguity

namespace SkinCareAiIntegration.ForAndroid
{
    public partial class CameraLivePage : ContentPage
    {
        private CascadeClassifier _faceCascade;
        private bool _isProcessing = false;
        private Mat _currentFrame = new Mat();

        public CameraLivePage()
        {
            InitializeComponent();
            BindingContext = new CameraViewModel();
            LoadHaarCascade();
            StartCamera();
        }

        private void LoadHaarCascade()
        {
            try
            {
                // Load from embedded resource
                var assembly = GetType().Assembly;
                using var stream = assembly.GetManifestResourceStream("SkinCareAiIntegration.Resources.haarcascade_frontalface_default.xml");

                if (stream == null)
                    throw new FileNotFoundException("Haar Cascade file not found");

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                _faceCascade = new CascadeClassifier(memoryStream.ToArray());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load Haar Cascade: {ex.Message}");
                DisplayAlert("Error", "Failed to load face detection model", "OK");
            }
        }

        private void StartCamera()
        {
            if (Camera != null)
            {
                Camera.CamerasLoaded += OnCamerasLoaded;
            }
        }

        private void OnCamerasLoaded(object sender, EventArgs e)
        {
            Camera.MediaCaptured += OnMediaCaptured;
        }

        private void OnMediaCaptured(object sender, MediaCapturedEventArgs e)
        {
            if (_isProcessing || _faceCascade == null) return;

            _isProcessing = true;

            try
            {
                // Convert frame to Emgu.CV Mat
                using (var ms = new MemoryStream(e.ImageData))
                using (var bitmap = new Bitmap(ms))
                {
                    _currentFrame = bitmap.ToMat();

                    // Face detection pipeline
                    var grayFrame = new Mat();
                    CvInvoke.CvtColor(_currentFrame, grayFrame, ColorConversion.Bgr2Gray);
                    CvInvoke.EqualizeHist(grayFrame, grayFrame);

                    var faces = _faceCascade.DetectMultiScale(
                        grayFrame,
                        scaleFactor: 1.1,
                        minNeighbors: 5,
                        minSize: new Size(30, 30));

                    // Update UI on main thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FaceCountLabel.Text = $"Faces Detected: {faces.Length}";
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Processing error: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (Camera != null)
            {
                Camera.CamerasLoaded -= OnCamerasLoaded;
                Camera.MediaCaptured -= OnMediaCaptured;
            }
            _currentFrame?.Dispose();
            _faceCascade?.Dispose();
        }
    }


}
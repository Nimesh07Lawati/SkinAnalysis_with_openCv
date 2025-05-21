using SkinCareAiIntegration.Services;

namespace SkinCareAiIntegration.ForAndroid
{
    public partial class AndroidCameraView : ContentPage
    {

        private readonly IDetectService _myserviceclient;

        public AndroidCameraView()
        {
            InitializeComponent();
            _myserviceclient = (IDetectService)(new DetectService());
        }
        public static byte[] ConvertStreamToBytes(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Denied", "Camera access is required to capture photos.", "OK");
                return;
            }

            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

                if (photo != null)
                {
                    // Save the file into local storage
                    string localFilePath = System.IO.Path.Combine(FileSystem.CacheDirectory, photo.FileName);

                    using Stream sourceStream = await photo.OpenReadAsync();
                    using FileStream localFileStream = File.OpenWrite(localFilePath);

                    await sourceStream.CopyToAsync(localFileStream);

                    // Flush and close the streams
                    await localFileStream.FlushAsync();
                    localFileStream.Close();

                    using Stream imageStream = File.OpenRead(localFilePath);


                    byte[] imageBytes = ConvertStreamToBytes(imageStream);


                    var detectResult = this._myserviceclient.Detect(imageBytes);
                    if (detectResult == null)
                        return;

                    if (detectResult.Boxes.Count > 0)
                    {
                        await App.Current.MainPage.DisplayAlert("Alert", "Human Face Detected and no of face detected " + detectResult.Boxes.Count, "Ok");
                    }
                    else
                    {
                        await App.Current.MainPage.DisplayAlert("Alert", "No Human Face Detected", "Ok");
                    }
                }
            }
        }

    }
}
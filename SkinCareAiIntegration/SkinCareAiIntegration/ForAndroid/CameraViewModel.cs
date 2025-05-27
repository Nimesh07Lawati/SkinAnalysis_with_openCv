using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace SkinCareAiIntegration.ForAndroid
{

    public partial class CameraViewModel : ObservableObject
    {
        [ObservableProperty]
        private CameraOptions _currentCamera = CameraOptions.Rear;

        [RelayCommand]
        private void ToggleCamera()
        {
            CurrentCamera = CurrentCamera == CameraOptions.Rear
                ? CameraOptions.Front
                : CameraOptions.Rear;

            Debug.WriteLine($"Camera switched to: {CurrentCamera}");
        }
    }
}
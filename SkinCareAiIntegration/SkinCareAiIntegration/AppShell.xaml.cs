using SkinCareAiIntegration.ForAndroid;

namespace SkinCareAiIntegration
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(CameraLivePage), typeof(CameraLivePage));
        }
    }
}

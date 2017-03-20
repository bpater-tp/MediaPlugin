using Plugin.Media.Abstractions;
using Xamarin.Forms;

namespace MediaTest
{
    public partial class App : Application
    {
        public static Location Location;
        public static bool AreLocationServicesEnabled = true;

        public App()
        {
            InitializeComponent();

            MainPage = new MediaTestPage();
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}

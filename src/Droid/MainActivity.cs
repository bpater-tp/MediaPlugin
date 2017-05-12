using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Locations;

namespace MediaTest.Droid
{
    [Activity(Label = "MediaTest.Droid", Icon = "@drawable/icon", Theme = "@style/MyTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity, ILocationListener
    {
        private LocationManager _locationManager;
        private string _provider;

        protected override void OnCreate(Bundle bundle)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);

            global::Xamarin.Forms.Forms.Init(this, bundle);

            _locationManager = GetSystemService(Context.LocationService) as LocationManager;
            LoadApplication(new App());
        }

        protected override void OnPause()
        {
            base.OnPause();
            _locationManager.RemoveUpdates(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _locationManager.RemoveUpdates(this);
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (string.IsNullOrEmpty(_provider))
            {
                FindBestProvider();
            }
            else
            {
                _locationManager.RequestLocationUpdates(_provider, 3000, 10, this);
            }
        }

        protected void FindBestProvider()
        {
            Criteria locCriteria = new Criteria()
            {
                Accuracy = Accuracy.Medium,
                PowerRequirement = Power.Medium,
            };

            _provider = _locationManager.GetBestProvider(locCriteria, true);
            //App.Provider = _provider;

            if (!string.IsNullOrEmpty(_provider))
            {
                App.AreLocationServicesEnabled = true;
                _locationManager.RequestLocationUpdates(_provider, 3000, 10, this);
            }
            else
            {
                App.AreLocationServicesEnabled = false;
                Console.WriteLine("No location services enabled");
            }
        }

        public void OnProviderEnabled(string provider)
        {
            if (provider == _provider)
            {
                App.AreLocationServicesEnabled = true;
            }
        }

        public void OnProviderDisabled(string provider)
        {
            if (provider == _provider)
            {
                FindBestProvider();
            }
        }

        public void OnStatusChanged(string provider, Availability status, Bundle extras)
        {
            if (_provider == provider && status != Availability.Available)
            {
                FindBestProvider();
            }
            else if (provider == LocationManager.GpsProvider && status == Availability.Available)
            {
                FindBestProvider();
            }
        }

        public void OnLocationChanged(Location location)
        {
            App.Location.Altitude = location.Altitude;
            App.Location.Latitude = location.Latitude;
            App.Location.Longitude = location.Longitude;
            App.Location.Speed = location.Speed;
            App.Location.HorizontalAccuracy = location.Accuracy;
            App.Location.Direction = location.Bearing;
            App.Location.Timestamp = new DateTime(location.Time);
        }
    }
}

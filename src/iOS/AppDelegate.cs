using System;
using System.Linq;
using CoreLocation;
using Foundation;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

namespace MediaTest.iOS
{
    [Register("AppDelegate")]
    public partial class AppDelegate : FormsApplicationDelegate
    {
        private CLLocationManager _locationMgr;

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            Forms.Init();

            LoadApplication(new App());
            StartLocation();

            return base.FinishedLaunching(app, options);
        }

        private void StartLocation()
        {
            _locationMgr = new CLLocationManager();
            _locationMgr.RequestWhenInUseAuthorization();
            if (!CLLocationManager.LocationServicesEnabled)
            {
                App.AreLocationServicesEnabled = false;
                Console.WriteLine("nie ukryjesz się");
                return;
            }
            _locationMgr.StartUpdatingLocation();
            _locationMgr.LocationsUpdated += (sender, e) =>
            {
                if (e.Locations.Any())
                {
                    App.Location.Latitude = e.Locations[0].Coordinate.Latitude;
                    App.Location.Longitude = e.Locations[0].Coordinate.Longitude;
                    App.Location.Altitude = e.Locations[0].Altitude;
                    App.Location.Speed = e.Locations[0].Speed;
                    App.Location.Direction = e.Locations[0].Course;
                    App.Location.Timestamp = e.Locations[0].Timestamp.ToDateTime();
                    var s = e.Locations[0].Coordinate.ToString();
                }
            };
        }

        public override void WillEnterForeground(UIApplication uiApplication)
        {
            base.WillEnterForeground(uiApplication);
            if (App.AreLocationServicesEnabled)
            {
                _locationMgr.StartUpdatingLocation();
            }
        }

        public override void DidEnterBackground(UIApplication uiApplication)
        {
            base.DidEnterBackground(uiApplication);
            if (App.AreLocationServicesEnabled)
            {
                _locationMgr.StopUpdatingLocation();
            }
        }
    }
}

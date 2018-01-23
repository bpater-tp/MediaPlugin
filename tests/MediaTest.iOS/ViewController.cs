using Foundation;
using Plugin.Media;
using System;
using System.Linq;

using UIKit;
using Plugin.Media.Abstractions;

namespace MediaTest.iOS
{
    public partial class ViewController : UIViewController
    {
        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            TakePhoto.Enabled = CrossMedia.Current.IsTakePhotoSupported;
            PickPhoto.Enabled = CrossMedia.Current.IsPickPhotoSupported;

            TakeVideo.Enabled = CrossMedia.Current.IsTakeVideoSupported;
            PickVideo.Enabled = CrossMedia.Current.IsPickVideoSupported;

            var fakeLocation = new Location {
                Altitude = 100,
                Direction = 10,
                HorizontalAccuracy = 1.0,
                Latitude = 53.31254,
                Longitude = 14.23423,
                Speed = 4,
                Timestamp = new DateTime()
            };

            TakePhoto.TouchUpInside += async (sender, args) =>
            {
                Func<object> func = CreateOverlay;
                var test = await CrossMedia.Current.TakePhotoAsync(new Plugin.Media.Abstractions.StoreCameraMediaOptions
                {
                    Name = "test1.jpg",
                    SaveToAlbum = AlbumSwitch.On,
                    PhotoSize = SizeSwitch.On ? Plugin.Media.Abstractions.PhotoSize.Medium : Plugin.Media.Abstractions.PhotoSize.Full,
                    OverlayViewProvider = OverlaySwitch.On ? func : null,
                    AllowCropping = CroppingSwitch.On,
                    CompressionQuality = (int)SliderQuality.Value,
                    Directory = "Sample",
                    DefaultCamera = FrontSwitch.On ? Plugin.Media.Abstractions.CameraDevice.Front : Plugin.Media.Abstractions.CameraDevice.Rear,
                    Location = fakeLocation,
                });

                if (test == null)
                    return;
                Console.WriteLine($"{test.MediaTakenAt}\n{test.Orientation}\n{test.Latitude}{test.LatitudeRef} {test.Longitude}{test.LongitudeRef}\n");

                var stream = test.GetStream();
                using (var data = NSData.FromStream(stream))
                    MainImage.Image = UIImage.LoadFromData(data);

                test.Dispose();
            };

            PickPhoto.TouchUpInside += async (sender, args) =>
            {
                var test = await CrossMedia.Current.PickPhotoAsync(
                    new Plugin.Media.Abstractions.PickMediaOptions
                    {
                        PhotoSize = SizeSwitch.On ? Plugin.Media.Abstractions.PhotoSize.Medium : Plugin.Media.Abstractions.PhotoSize.Full,
                        CompressionQuality = (int)SliderQuality.Value
                    });
                if (test == null)
                    return;

                foreach (var t in test) {
                    Console.WriteLine(t.Path);
                    Console.WriteLine($"{t.MediaTakenAt}\n{t.Orientation}\n{t.Latitude}{t.LatitudeRef} {t.Longitude}{t.LongitudeRef}\n");
                }
                //new UIAlertView("Success", mediafile.Path, null, "OK").Show();

                int i = 0;
                var images = new[] {MainImage, MainImage1, MainImage2, MainImage3, MainImage4, MainImage5};
                foreach(var mediafile in test)
                {
                    var image = images[i++];
                    if (i > 6) continue;
                    var stream = mediafile.GetStream();
                    using (var data = NSData.FromStream(stream))
                        image.Image = UIImage.LoadFromData(data);

                    mediafile.Dispose();
                }
            };

            TakeVideo.TouchUpInside += async (sender, args) =>
            {
                var test = await CrossMedia.Current.TakeVideoAsync(new Plugin.Media.Abstractions.StoreVideoOptions
                {
                    Name = "test1.mp4",
                    SaveToAlbum = true
                });

                if (test == null)
                    return;

                new UIAlertView("Success", test.Path, null, "OK").Show();

                test.Dispose();
            };

            PickVideo.TouchUpInside += async (sender, args) =>
            {
                var test = await CrossMedia.Current.PickVideoAsync();
                if (test == null)
                    return;

                new UIAlertView("Success", test.Path, null, "OK").Show();

                test.Dispose();
            };
            // Perform any additional setup after loading the view, typically from a nib.
        }

        public object CreateOverlay()
        {
            var imageView = new UIImageView(UIImage.FromBundle("face-template.png"));
            imageView.ContentMode = UIViewContentMode.ScaleAspectFit;

            var screen = UIScreen.MainScreen.Bounds;
            imageView.Frame = screen;

            return imageView;
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
        }
    }
}
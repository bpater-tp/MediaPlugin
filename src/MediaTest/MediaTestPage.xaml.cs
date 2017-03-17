using System;
using System.Reflection;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Xamarin.Forms;

namespace MediaTest
{
    public partial class MediaTestPage : ContentPage
    {
        public MediaTestPage()
        {
            InitializeComponent();
            CrossMedia.Current.Initialize();
        }

        public async void TakePhoto(object sender, EventArgs args)
        {
            var photo = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions()
            {
                Name = "photo.jpg",
                PhotoSize = PhotoSize.Full,
                SaveToAlbum = false
            });
            var stream = photo.GetStream();
            ExaminePhoto(stream);
        }

        public async void PickPhoto(object sender, EventArgs args)
        {
            var photo = await CrossMedia.Current.PickPhotoAsync();
            var stream = photo.GetStream();
            ExaminePhoto(stream);
        }

        private void ExaminePhoto(System.IO.Stream stream)
        {
            var exif = ExifLib.ExifReader.ReadJpeg(stream);
            string exif_string = $"Dimenssions: {exif.Width}x{exif.Height}\n" +
                                 $"Date taken: {exif.DateTime}\n" +
                                 $"Camera model: {exif.Model}\n" +
                                 $"Camera maker: {exif.Make}\n" +
                                 $"Orientation: {exif.Orientation}\n" +
                                 $"GPS Lat: {exif.GpsLatitude[0]}'{exif.GpsLatitude[1]}\"{exif.GpsLatitude[2]}\n" +
                                 $"GPS Long: {exif.GpsLongitude[0]}'{exif.GpsLongitude[1]}\"{ exif.GpsLongitude[2]}";
            DisplayAlert("photo", exif_string, "ok");
        }

    }
}

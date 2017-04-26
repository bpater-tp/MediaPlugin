using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CoreImage;
using Foundation;
using Photos;

namespace Plugin.Media
{
    public class PhotoLibraryAccess
    {
        private static TaskCompletionSource<NSDictionary> taskCompleted;

        public static Task<NSDictionary> GetPhotoLibraryMetadata(NSUrl url)
        {
            taskCompleted = new TaskCompletionSource<NSDictionary>();
            NSDictionary meta = null;

            var options = new PHContentEditingInputRequestOptions
            {
                NetworkAccessAllowed = true,
            };
            NSUrl[] urls = { url };
            var image = PHAsset.FetchAssets(urls, new PHFetchOptions()).firstObject as PHAsset;

            var t = Task.Run(() =>
            {
                image.RequestContentEditingInput(options, (contentEditingInput, requestStatusInfo) =>
                {
                    try
                    {
                        var fullimage = CIImage.FromUrl(contentEditingInput.FullSizeImageUrl);
                        if (fullimage?.Properties != null)
                        {
                            meta = new NSMutableDictionary();
                            meta[ImageIO.CGImageProperties.Orientation] = new NSString(fullimage.Properties.Orientation.ToString());
                            meta[ImageIO.CGImageProperties.ExifDictionary] = fullimage.Properties.Exif?.Dictionary ?? new NSDictionary();
                            meta[ImageIO.CGImageProperties.TIFFDictionary] = fullimage.Properties.Tiff?.Dictionary ?? new NSDictionary();
                            meta[ImageIO.CGImageProperties.GPSDictionary] = fullimage.Properties.Gps?.Dictionary ?? new NSDictionary();
                            meta[ImageIO.CGImageProperties.IPTCDictionary] = fullimage.Properties.Iptc?.Dictionary ?? new NSDictionary();
                            meta[ImageIO.CGImageProperties.JFIFDictionary] = fullimage.Properties.Jfif?.Dictionary ?? new NSDictionary();
                        }
                        taskCompleted.SetResult(meta);
                    }
                    catch (Exception ex)
                     {
                        taskCompleted.TrySetException(ex);
                    }
                });
            });

            Task.WhenAny(new Task[] { t, Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith((arg) =>  taskCompleted.SetCanceled()) });

            return taskCompleted.Task;
        }
    }
}

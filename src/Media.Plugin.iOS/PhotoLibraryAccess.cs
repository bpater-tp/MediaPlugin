using System;
using System.Threading;
using System.Threading.Tasks;
using CoreImage;
using Foundation;
using Photos;

namespace Plugin.Media
{
    public class PhotoLibraryAccess
    {
        public static async Task<NSDictionary> GetPhotoLibraryMetadata(NSUrl url)
        {
            NSDictionary meta = null;
            var completed = false;
            var options = new PHContentEditingInputRequestOptions
            {
                NetworkAccessAllowed = true,
            };
            NSUrl[] urls = { url };
            var image = PHAsset.FetchAssets(urls, new PHFetchOptions()).firstObject as PHAsset;
            await Task.Run(() =>
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
                            meta[ImageIO.CGImageProperties.ExifDictionary] = fullimage.Properties.Exif?.Dictionary;
                            meta[ImageIO.CGImageProperties.TIFFDictionary] = fullimage.Properties.Tiff?.Dictionary;
                            meta[ImageIO.CGImageProperties.GPSDictionary] = fullimage.Properties.Gps?.Dictionary;
                            meta[ImageIO.CGImageProperties.IPTCDictionary] = fullimage.Properties.Iptc?.Dictionary;
                            meta[ImageIO.CGImageProperties.JFIFDictionary] = fullimage.Properties.Jfif?.Dictionary;
                       }
                       completed = true;
                    }
                    catch (Exception ex)
                    {
                       completed = true;
                    }
                });
                while (!completed)
                {
                    Thread.Sleep(10);
                }
            });

            return meta;
        }
    }
}

using Plugin.Media.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using CoreGraphics;
using CoreImage;
using UIKit;
using Foundation;
using GMImagePicker;
using ImageIO;
using MobileCoreServices;
using Photos;

namespace Plugin.Media
{
    /// <summary>
    /// Implementation for Media
    /// </summary>
    public class MediaImplementation : IMedia
    {
        /// <summary>
        /// Color of the status bar
        /// </summary>
        public static UIStatusBarStyle StatusBarStyle { get; set; }

       

        ///<inheritdoc/>
        public Task<bool> Initialize() => Task.FromResult(true);

        private static int _pickerDelegateCount = 1;

        /// <summary>
        /// Implementation
        /// </summary>
        public MediaImplementation()
        {
            StatusBarStyle = UIApplication.SharedApplication.StatusBarStyle;
            IsCameraAvailable = UIImagePickerController.IsCameraDeviceAvailable(UIKit.UIImagePickerControllerCameraDevice.Front)
                               | UIImagePickerController.IsCameraDeviceAvailable(UIKit.UIImagePickerControllerCameraDevice.Rear);

            var availableCameraMedia = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.Camera) ?? new string[0];
            var avaialbleLibraryMedia = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.PhotoLibrary) ?? new string[0];

            foreach (string type in availableCameraMedia.Concat(avaialbleLibraryMedia))
            {
                if (type == TypeMovie)
                    IsTakeVideoSupported = IsPickVideoSupported = true;
                else if (type == TypeImage)
                    IsTakePhotoSupported = IsPickPhotoSupported = true;
            }
        }
        /// <inheritdoc/>
        public bool IsCameraAvailable { get; }

        /// <inheritdoc/>
        public bool IsTakePhotoSupported { get; }

        /// <inheritdoc/>
        public bool IsPickPhotoSupported { get; }

        /// <inheritdoc/>
        public bool IsTakeVideoSupported { get; }

        /// <inheritdoc/>
        public bool IsPickVideoSupported { get; }

        
        /// <summary>
        /// Picks a photo from the default gallery
        /// </summary>
        /// <returns>Media file or null if canceled</returns>
        public Task<List<MediaFile>> PickPhotoAsync(PickMediaOptions options = null)
        {
            if (!IsPickPhotoSupported)
                throw new NotSupportedException();

            CheckPhotoUsageDescription();

            return PickMultiplePhotos(options);
        }
 

        /// <summary>
        /// Take a photo async with specified options
        /// </summary>
        /// <param name="options">Camera Media Options</param>
        /// <returns>Media file of photo or null if canceled</returns>
        public Task<MediaFile> TakePhotoAsync(StoreCameraMediaOptions options)
        {
            if (!IsTakePhotoSupported)
                throw new NotSupportedException();
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            CheckCameraUsageDescription();

            VerifyCameraOptions(options);

            return GetMediaAsync(UIImagePickerControllerSourceType.Camera, TypeImage, options);
        }
     

        /// <summary>
        /// Picks a video from the default gallery
        /// </summary>
        /// <returns>Media file of video or null if canceled</returns>
        public Task<MediaFile> PickVideoAsync()
        {
            if (!IsPickVideoSupported)
                throw new NotSupportedException();


            CheckPhotoUsageDescription();

            return GetMediaAsync(UIImagePickerControllerSourceType.PhotoLibrary, TypeMovie);
        }
        

        /// <summary>
        /// Take a video with specified options
        /// </summary>
        /// <param name="options">Video Media Options</param>
        /// <returns>Media file of new video or null if canceled</returns>
        public Task<MediaFile> TakeVideoAsync(StoreVideoOptions options)
        {
            if (!IsTakeVideoSupported)
                throw new NotSupportedException();
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            CheckCameraUsageDescription();

            VerifyCameraOptions(options);

            return GetMediaAsync(UIImagePickerControllerSourceType.Camera, TypeMovie, options);
        }

        private UIPopoverController popover;
        private UIImagePickerControllerDelegate pickerDelegate;
        /// <summary>
        /// image type
        /// </summary>
        public const string TypeImage = "public.image";
        /// <summary>
        /// movie type
        /// </summary>
        public const string TypeMovie = "public.movie";

        private void VerifyOptions(StoreMediaOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (options.Directory != null && Path.IsPathRooted(options.Directory))
                throw new ArgumentException("options.Directory must be a relative path", "options");
        }

        private void VerifyCameraOptions(StoreCameraMediaOptions options)
        {
            VerifyOptions(options);
            if (!Enum.IsDefined(typeof(CameraDevice), options.DefaultCamera))
                throw new ArgumentException("options.Camera is not a member of CameraDevice");
        }

        private static MediaPickerController SetupController(MediaPickerDelegate mpDelegate, UIImagePickerControllerSourceType sourceType, string mediaType, StoreCameraMediaOptions options = null)
        {
            var picker = new MediaPickerController(mpDelegate);
            picker.MediaTypes = new[] { mediaType };
            picker.SourceType = sourceType;

            if (sourceType == UIImagePickerControllerSourceType.Camera)
            {
                picker.CameraDevice = GetUICameraDevice(options.DefaultCamera);
                picker.AllowsEditing = options?.AllowCropping ?? false;

                if (options.OverlayViewProvider != null)
                {
                    var overlay = options.OverlayViewProvider();
                    if (overlay is UIView)
                    {
                        picker.CameraOverlayView = overlay as UIView;
                    }
                }
                if (mediaType == TypeImage)
                {
                    picker.CameraCaptureMode = UIImagePickerControllerCameraCaptureMode.Photo;
                }
                else if (mediaType == TypeMovie)
                {
                    StoreVideoOptions voptions = (StoreVideoOptions)options;

                    picker.CameraCaptureMode = UIImagePickerControllerCameraCaptureMode.Video;
                    picker.VideoQuality = GetQuailty(voptions.Quality);
                    picker.VideoMaximumDuration = voptions.DesiredLength.TotalSeconds;
                }
            }

            return picker;
        }

        private Task<MediaFile> GetMediaAsync(UIImagePickerControllerSourceType sourceType, string mediaType, StoreCameraMediaOptions options = null)
        {
			
			var viewController = FindRootViewController();

            MediaPickerDelegate ndelegate = new MediaPickerDelegate(viewController, sourceType, options);
            var od = Interlocked.CompareExchange(ref pickerDelegate, ndelegate, null);
            if (od != null)
                throw new InvalidOperationException("Only one operation can be active at at time");

            var picker = SetupController(ndelegate, sourceType, mediaType, options);

            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad && sourceType == UIImagePickerControllerSourceType.PhotoLibrary)
            {
                ndelegate.Popover = new UIPopoverController(picker);
                ndelegate.Popover.Delegate = new MediaPickerPopoverDelegate(ndelegate, picker);
                ndelegate.DisplayPopover();
            }
            else
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0))
                {
                    picker.ModalPresentationStyle = UIModalPresentationStyle.OverCurrentContext;
                }
                viewController.PresentViewController(picker, true, null);
            }

            return ndelegate.Task.ContinueWith(t =>
            {
                if (popover != null)
                {
                    popover.Dispose();
                    popover = null;
                }

                Interlocked.Exchange(ref pickerDelegate, null);
                return t;
            }).Unwrap();
        }

        private static UIViewController FindRootViewController()
        {
            UIViewController viewController = null;
            UIWindow window = UIApplication.SharedApplication.KeyWindow;
            if (window == null)
                throw new InvalidOperationException("There's no current active window");

            if (window.WindowLevel == UIWindowLevel.Normal)
                viewController = window.RootViewController;

            if (viewController == null)
            {
                window = UIApplication.SharedApplication.Windows.OrderByDescending(w => w.WindowLevel)
                    .FirstOrDefault(w => w.RootViewController != null && w.WindowLevel == UIWindowLevel.Normal);
                if (window == null)
                    throw new InvalidOperationException("Could not find current view controller");

                viewController = window.RootViewController;
            }

            while (viewController.PresentedViewController != null)
                viewController = viewController.PresentedViewController;
            return viewController;
        }

        public Task<List<MediaFile>> PickMultiplePhotos(PickMediaOptions options = null)
        {
            var tcs = new TaskCompletionSource<Task<List<MediaFile>>>();

            var colsInPortrait = 3;
            var colsInLandscape = 5;

            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
            {
                colsInPortrait = 6;
                colsInLandscape = 8;
            }
            var picker = new GMImagePickerController
            {
                Title = options?.Title ?? "Photos",
                MediaTypes = new[] {PHAssetMediaType.Image},
                ColsInPortrait = colsInPortrait,
                ColsInLandscape = colsInLandscape,
                CustomSmartCollections = new[] {
                    PHAssetCollectionSubtype.AlbumRegular,
                    PHAssetCollectionSubtype.AlbumImported,
                    PHAssetCollectionSubtype.SmartAlbumGeneric,
                    PHAssetCollectionSubtype.SmartAlbumLivePhotos,
                    PHAssetCollectionSubtype.SmartAlbumPanoramas,
                    PHAssetCollectionSubtype.SmartAlbumRecentlyAdded,
                    PHAssetCollectionSubtype.SmartAlbumUserLibrary,
                    PHAssetCollectionSubtype.AlbumCloudShared,
                    PHAssetCollectionSubtype.SmartAlbumFavorites,
                },
                ShowCameraButton = true,
                AllowsEditingCameraImages = true,
            };
            picker.FinishedPickingAssets += (sender, args) =>
            {
                var task = Task.Run(() =>
                {
                    var images = new List<MediaFile>();
                    foreach (var asset in args.Assets)
                    {
                        var path = StorePickedImage(asset, options?.CompressionQuality ?? 90, GetScale(options?.PhotoSize ?? PhotoSize.Full), options.RotateImage);
                        images.Add(new MediaFile(path, () => File.OpenRead(path)));
                    }
                    return images;
                });
                tcs.TrySetResult(task);
            };
            picker.Canceled += (sender, args) => tcs.TrySetResult(null);

            var od = Interlocked.CompareExchange(ref _pickerDelegateCount, 1, 1);
            if (od != 1)
            {
                throw new InvalidOperationException("Only one operation can be active at at time");
            }

            var viewController = FindRootViewController();
            viewController.PresentViewController(picker, true, null);
            _pickerDelegateCount++;

            return tcs.Task.ContinueWith(t =>
            {
                Interlocked.Exchange(ref _pickerDelegateCount, 1);
                if (t != null)
                {
                    t.Wait();
                    try
                    {
                        return t.Result?.Result;
                    }
                    catch (Exception ex)
                    {
                        if(ex.InnerException != null)
                        {
                            throw ex.InnerException;
                        }
                    }
                }

                return null;
            });
        }

        private static string StorePickedImage(PHAsset asset, int quality, float scale, bool rotate)
        {
            var imageManager = PHImageManager.DefaultManager;
            var requestOptions = new PHImageRequestOptions
            {
                Synchronous = true,
                NetworkAccessAllowed = true,
                DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
            };
            var targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string path = null;
            imageManager.RequestImageData(asset, requestOptions, (data, dataUti, orientation, info) =>
            {
                NSError error = (NSError)info["PHImageErrorKey"];
                if (error != null) {
                    var description = error.LocalizedDescription;
                    var reason = error.UserInfo.ValueForKey((NSString)"NSUnderlyingError");
                    throw new Exception($"{description}. {reason}");
                }
                if (info["PHImageFileURLKey"] is NSUrl url)
                {
                    path = Path.Combine(targetDir, url.LastPathComponent);
                }
                else
                {
                    var parts = asset.LocalIdentifier.Split('/');
                    path = Path.Combine(targetDir, parts[0], ".jpg");
                }
                var fullimage = CIImage.FromData(data);
                var image = UIImage.LoadFromData(data);
                var scaledImage = image.ScaleImage(scale);
                if (rotate)
                {
                    scaledImage = scaledImage.RotateImage();
                }
                SaveTempImage(fullimage, scaledImage, path, quality, rotate);
            });

            return path;
        }

        private static void SaveTempImage(CIImage fullimage, UIImage image, string outputFilename, int quality, bool rotate)
        {
            var imageData = image.AsJPEG(quality);
            var dataProvider = new CGDataProvider(imageData);
            var cgImageFromJpeg = CGImage.FromJPEG(dataProvider, null, false, CGColorRenderingIntent.Default);
            var imageWithExif = new NSMutableData();
            var destination = CGImageDestination.Create(imageWithExif, UTType.JPEG, 1);
            var cgImageMetadata = new CGMutableImageMetadata();
            var options = new CGImageDestinationOptions();
            if (fullimage.Properties.DPIWidthF != null)
            {
                options.Dictionary[ImageIO.CGImageProperties.DPIWidth] =
                    new NSNumber((nfloat)fullimage.Properties.DPIWidthF);
            }
            if (fullimage.Properties.DPIWidthF != null)
            {
                options.Dictionary[ImageIO.CGImageProperties.DPIHeight] =
                    new NSNumber((nfloat)fullimage.Properties.DPIHeightF);
            }
            options.ExifDictionary = fullimage.Properties?.Exif ?? new CGImagePropertiesExif();
            options.TiffDictionary = fullimage.Properties?.Tiff ?? new CGImagePropertiesTiff();
            options.GpsDictionary = fullimage.Properties?.Gps ?? new CGImagePropertiesGps();
            options.JfifDictionary = fullimage.Properties?.Jfif ?? new CGImagePropertiesJfif();
            options.IptcDictionary = fullimage.Properties?.Iptc ?? new CGImagePropertiesIptc();
            if (rotate) {
                options.Dictionary[ImageIO.CGImageProperties.Orientation] =
                           new NSString(UIImageOrientation.Up.ToString());
                var tiffDict = new CGImagePropertiesTiff();
                foreach(KeyValuePair<NSObject, NSObject> x in options.TiffDictionary.Dictionary)
                {
                    tiffDict.Dictionary.SetValueForKey(x.Value, x.Key as NSString);
                }
                tiffDict.Dictionary.SetValueForKey(new NSNumber(1), new NSString("Orientation"));
                options.TiffDictionary = tiffDict;
            } else {
                if (fullimage.Properties.Orientation != null)
                {
                    options.Dictionary[ImageIO.CGImageProperties.Orientation] =
                               new NSString(image.Orientation.ToString());
                }
            }
            destination.AddImageAndMetadata(cgImageFromJpeg, cgImageMetadata, options);
            var success = destination.Close();
            if (success)
            {
                imageWithExif.Save(outputFilename, true);
            }
            else
            {
                imageData.Save(outputFilename, true);
            }
        }

        private static float GetScale(PhotoSize size)
        {
            var scaleMap = new Dictionary<PhotoSize, float>{
                {PhotoSize.Custom, 2.0f},
                {PhotoSize.Full,   1.0f},
                {PhotoSize.Large,  0.75f},
                {PhotoSize.Medium, 0.5f},
                {PhotoSize.Small,  0.25f},
            };
            return scaleMap[size];
        }

        private static UIImagePickerControllerCameraDevice GetUICameraDevice(CameraDevice device)
        {
            switch (device)
            {
                case CameraDevice.Front:
                    return UIImagePickerControllerCameraDevice.Front;
                case CameraDevice.Rear:
                    return UIImagePickerControllerCameraDevice.Rear;
                default:
                    throw new NotSupportedException();
            }
        }

        private static UIImagePickerControllerQualityType GetQuailty(VideoQuality quality)
        {
            switch (quality)
            {
                case VideoQuality.Low:
                    return UIImagePickerControllerQualityType.Low;
                case VideoQuality.Medium:
                    return UIImagePickerControllerQualityType.Medium;
                default:
                    return UIImagePickerControllerQualityType.High;
            }
        }


        void CheckCameraUsageDescription()
        {
            var info = NSBundle.MainBundle.InfoDictionary;

            if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
            {
                if (!info.ContainsKey(new NSString("NSCameraUsageDescription")))
                    throw new UnauthorizedAccessException("On iOS 10 and higher you must set NSCameraUsageDescription in your Info.plist file to enable Authorization Requests for Camera access!");
            }
        }

        void CheckPhotoUsageDescription()
        {
            var info = NSBundle.MainBundle.InfoDictionary;

            if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
            {
                if (!info.ContainsKey(new NSString("NSPhotoLibraryUsageDescription")))
                    throw new UnauthorizedAccessException("On iOS 10 and higher you must set NSPhotoLibraryUsageDescription in your Info.plist file to enable Authorization Requests for Photo Library access!");
            }
        }
    }
}

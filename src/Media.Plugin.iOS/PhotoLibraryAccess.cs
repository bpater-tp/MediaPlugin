using System;
using System.Linq;
using CoreImage;
using Foundation;
using Photos;
using UIKit;

namespace Plugin.Media
{
	/// <summary>
	/// Accesst library
	/// </summary>
	public static class PhotoLibraryAccess
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static (NSDictionary, string) GetPhotoLibraryMetadata(NSUrl url)
		{
			NSDictionary meta = null;

			var image = PHAsset.FetchAssets(new NSUrl[] { url }, new PHFetchOptions()).firstObject as PHAsset;
			var imageManager = PHImageManager.DefaultManager;
            using (var requestOptions = new PHImageRequestOptions
            {
                Synchronous = true,
                NetworkAccessAllowed = true,
                DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
            })
            {
                imageManager.RequestImageData(image, requestOptions, (data, dataUti, orientation, info) =>
			    {
			        try
			        {
			            var fullimage = CIImage.FromData(data);
			            if (fullimage?.Properties != null)
			            {
			                meta = new NSMutableDictionary
			                {
			                    [ImageIO.CGImageProperties.Orientation] = new NSString(fullimage.Properties.Orientation.ToString()),
			                    [ImageIO.CGImageProperties.ExifDictionary] = fullimage.Properties.Exif?.Dictionary ?? new NSDictionary(),
			                    [ImageIO.CGImageProperties.TIFFDictionary] = fullimage.Properties.Tiff?.Dictionary ?? new NSDictionary(),
			                    [ImageIO.CGImageProperties.GPSDictionary] = fullimage.Properties.Gps?.Dictionary ?? new NSDictionary(),
			                    [ImageIO.CGImageProperties.IPTCDictionary] = fullimage.Properties.Iptc?.Dictionary ?? new NSDictionary(),
			                    [ImageIO.CGImageProperties.JFIFDictionary] = fullimage.Properties.Jfif?.Dictionary ?? new NSDictionary()
			                };
			            }
			        }
			        catch (Exception ex)
			        {
			            Console.WriteLine(ex);
			        }

			    });
            }

			return (meta, image.LocalIdentifier);
		}

		public static bool SaveImageToGalery(string imagePath, string albumName)
		{
			var saved = true;
			PHAssetCollection customAlbum = null;
			if (!string.IsNullOrEmpty(albumName))
			{
				customAlbum = FindOrCreateAlbum(albumName);
				if (customAlbum == null)
				{
					return false;
				}
			}

			PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(
				() =>
				{
					var assetRequest = PHAssetChangeRequest.FromImage(NSUrl.FromFilename(imagePath));
					if (customAlbum != null)
					{
						var albumRequest = PHAssetCollectionChangeRequest.ChangeRequest(customAlbum);
						albumRequest?.AddAssets(new[] { assetRequest.PlaceholderForCreatedAsset });
					}
				},
				(success, error) =>
				{
					if (!success)
					{
						Console.WriteLine(error);
						saved = success;
					}
				}
			);
			return saved;
		}

		public static bool SaveVideoToGalery(NSUrl video, string path, string albumName)
		{
			var saved = true;
			//if (string.IsNullOrEmpty(albumName))
			//{
				UIVideo.SaveToPhotosAlbum(path, (path, error) =>
				{
					if (error != null)
					{
						saved = false;
						Console.WriteLine(error);
					}
				});
				return saved;
			//}
			//var compatible = UIVideo.IsCompatibleWithSavedPhotosAlbum(path);
			//var customAlbum = FindOrCreateAlbum(albumName);
			//if (customAlbum == null)
			//{
			//	return false;
			//}
			//PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(
			//	() =>
			//	{
			//		var assetRequest = PHAssetChangeRequest.FromVideo(video);
			//		var albumRequest = PHAssetCollectionChangeRequest.ChangeRequest(customAlbum);
			//		albumRequest?.AddAssets(new[] { assetRequest.PlaceholderForCreatedAsset });
			//	},
			//	(success, error) =>
			//	{
			//		if (!success)
			//		{
			//			Console.WriteLine(error);
			//			saved = success;
			//		}
			//	}
			//);
			//return saved;
		}

		private static PHAssetCollection FindOrCreateAlbum(string albumName)
		{
			var albums = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album, PHAssetCollectionSubtype.AlbumRegular, null);
			var customAlbum = (PHAssetCollection)albums.FirstOrDefault(s => ((PHAssetCollection)s).LocalizedTitle.Equals(albumName));
			if (customAlbum == null)
			{
				var success = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(
					() =>
					{
						PHAssetCollectionChangeRequest.CreateAssetCollection(albumName);
					}, out var error
				);
				if (success)
				{
					albums = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album, PHAssetCollectionSubtype.AlbumRegular, null);
					customAlbum = (PHAssetCollection)albums.FirstOrDefault(s => ((PHAssetCollection)s).LocalizedTitle.Equals(albumName));
				}
				else
				{
					Console.WriteLine(error);
					customAlbum = null;
				}
			}

			return customAlbum;
		}

		public static bool DeleteImagesFromGallery(string[] localIds)
		{
			var images = PHAsset.FetchAssetsUsingLocalIdentifiers(localIds, new PHFetchOptions());
			var assets = images.Select(a => (PHAsset)a).ToArray();
			var success = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait
			(
				() =>
				{
					PHAssetChangeRequest.DeleteAssets(assets);
				}, out var error
			);
			if (error != null)
			{
				Console.WriteLine(error);
			}
			return success;
		}


	}
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Plugin.Media.Abstractions;
using Android.Graphics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Android.Support.Media;
using System.Globalization;
using Permissions = Xamarin.Essentials.Permissions;
using PermissionStatus = Xamarin.Essentials.PermissionStatus;

namespace Plugin.Media
{
	/// <summary>
	/// Implementation for Feature
	/// </summary>
	[Android.Runtime.Preserve(AllMembers = true)]
	public class MediaImplementation : IMedia
	{
		private const string AllTypes = "*/*";
		/// <summary>
		/// Implementation
		/// </summary>
		public MediaImplementation()
		{

			this.context = Android.App.Application.Context;
			IsCameraAvailable = context.PackageManager.HasSystemFeature(PackageManager.FeatureCamera);

			if (Build.VERSION.SdkInt >= BuildVersionCodes.Gingerbread)
				IsCameraAvailable |= context.PackageManager.HasSystemFeature(PackageManager.FeatureCameraFront);
		}

		///<inheritdoc/>
		public Task<bool> Initialize() => Task.FromResult(true);

		/// <inheritdoc/>
		public bool IsCameraAvailable { get; }
		/// <inheritdoc/>
		public bool IsTakePhotoSupported => true;

		/// <inheritdoc/>
		public bool IsPickPhotoSupported => true;

		/// <inheritdoc/>
		public bool IsTakeVideoSupported => true;
		/// <inheritdoc/>
		public bool IsPickVideoSupported => true;


		/// <summary>
		/// Picks a photo from the default gallery
		/// </summary>
		/// <returns>Media file or null if canceled</returns>
		public async Task<List<MediaFile>> PickPhotoAsync(PickMediaOptions options = null)
		{
			if (!(await RequestStoragePermission()))
			{
				return null;
			}
			var mediaList = await TakeMediaAsync("image/*", Intent.ActionPick, null);
			return await HandleMediaFiles(mediaList, options);
		}


		private async Task<List<MediaFile>> HandleMediaFiles(List<MediaFile> mediaList, PickMediaOptions options = null)
		{
			if (mediaList == null)
				return null;

			if (options == null)
				options = new PickMediaOptions();

			//check to see if we picked a file, and if so then try to fix orientation and resize
			for (int i = 0; i < mediaList.Count; i++)
			{
				var media = mediaList[i];
				if (string.IsNullOrWhiteSpace(media?.Path) || media?.Type == Abstractions.MediaType.Video)
				{
					continue;
				}

				try
				{
					bool imageChanged = false;
					var originalMetadata = new ExifInterface(media.Path);
					if (options.RotateImage)
					{
						imageChanged = await FixOrientationAndResizeAsync(media.Path, options, originalMetadata);
					}
					else
					{
						imageChanged = await ResizeAsync(media.Path, options.PhotoSize, options.CompressionQuality, options.CustomPhotoSize, options.MaxWidthHeight, originalMetadata);
					}
					if (imageChanged)
					{
						var newExif = new ExifInterface(media.Path);
						newExif.Copy(originalMetadata);
						newExif.SaveAttributes();
					}
					else
					{
						originalMetadata.SaveAttributes();
					}
					UpdateMetadata(ref media, originalMetadata);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Unable to check orientation: " + ex);
				}
			}
			return mediaList;
		}

		/// <summary>
		/// Take a photo async with specified options
		/// </summary>
		/// <param name="options">Camera Media Options</param>
		/// <returns>Media file of photo or null if canceled</returns>
		public async Task<MediaFile> TakePhotoAsync(StoreCameraMediaOptions options)
		{
			if (!IsCameraAvailable)
				throw new NotSupportedException();

			if (!(await RequestCameraPermissions()))
			{
				return null;
			}


			VerifyOptions(options);

			var mediaList = (await TakeMediaAsync("image/*", MediaStore.ActionImageCapture, options));
			if (mediaList == null)
				return null;

			var media = mediaList.First();
			if (string.IsNullOrWhiteSpace(media?.Path))
				return media;

			//check to see if we need to rotate if success


			try
			{
				bool imageChanged = false;
				var exif = new ExifInterface(media.Path);
				exif.SetAttribute(ExifInterface.TagDatetime, DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"));
				exif.SetAttribute(ExifInterface.TagDatetimeOriginal, DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"));
				if (options.RotateImage)
				{
					imageChanged = await FixOrientationAndResizeAsync(media.Path, options, exif);
				}
				else
				{
					imageChanged = await ResizeAsync(media.Path, options.PhotoSize, options.CompressionQuality, options.CustomPhotoSize, options.MaxWidthHeight, exif);
				}
				SetMissingMetadata(exif, options.Location);
				if (imageChanged)
				{
					var newExif = new ExifInterface(media.Path);
					newExif.Copy(exif);
					newExif.SaveAttributes();
				}
				else
				{
					exif.SaveAttributes();
				}
				UpdateMetadata(ref media, exif);

				if (options.SaveToAlbum)
				{
					SaveMediaToAlbum(options, media);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Unable to check orientation: " + ex);
			}

			return media;
		}

		/// <summary>
		/// Picks a video from the default gallery
		/// </summary>
		/// <returns>Media file of video or null if canceled</returns>
		public async Task<MediaFile> PickVideoAsync()
		{

			if (!(await RequestStoragePermission()))
			{
				return null;
			}

			return (await TakeMediaAsync("video/*", Intent.ActionPick, null)).First();
		}

		/// <summary>
		/// Take a video with specified options
		/// </summary>
		/// <param name="options">Video Media Options</param>
		/// <returns>Media file of new video or null if canceled</returns>
		public async Task<MediaFile> TakeVideoAsync(StoreVideoOptions options)
		{
			Console.WriteLine($"save: {options.SaveToAlbum}\nsize: {options.PhotoSize}\n");
			if (!IsCameraAvailable)
				throw new NotSupportedException();

			if (!(await RequestCameraPermissions()))
			{
				return null;
			}

			VerifyOptions(options);

			var mediaList =await TakeMediaAsync("video/*", MediaStore.ActionVideoCapture, options);
			if (mediaList == null)
				return null;

			var media = mediaList.First();
			if (string.IsNullOrWhiteSpace(media?.Path))
				return media;

			media.Type = Abstractions.MediaType.Video;
			if (options.SaveToAlbum)
			{
				SaveMediaToAlbum(options, media);
			}
			return media;
		}

		private void SaveMediaToAlbum(StoreCameraMediaOptions options, MediaFile media)
		{
			var defImage = new Dictionary<string, string>
			{
				{"Title", MediaStore.Images.Media.InterfaceConsts.Title },
				{"Description", MediaStore.Images.Media.InterfaceConsts.Description },
				{"DateTaken", MediaStore.Images.Media.InterfaceConsts.DateTaken },
				{"BucketId", MediaStore.Images.ImageColumns.BucketId },
				{"BucketDisplayName", MediaStore.Images.ImageColumns.BucketDisplayName },
			};
			var defVideo = new Dictionary<string, string>
			{
				{"Title", MediaStore.Video.Media.InterfaceConsts.Title },
				{"Description", MediaStore.Video.Media.InterfaceConsts.Description },
				{"DateTaken", MediaStore.Video.Media.InterfaceConsts.DateTaken },
				{"BucketId", MediaStore.Video.VideoColumns.BucketId },
				{"BucketDisplayName", MediaStore.Video.VideoColumns.BucketDisplayName },
			};
			var definitions = new Dictionary<Abstractions.MediaType, Dictionary<string, string>>
			{
				{ Abstractions.MediaType.Image, defImage },
				{ Abstractions.MediaType.Video, defVideo },
			};

			try
			{
				Console.WriteLine("saving to gallery");
				var fileName = System.IO.Path.GetFileName(media.Path);
				var publicUri = MediaPickerActivity.GetOutputMediaFile(context, options.Directory ?? "temp", fileName, true, true);
				using (Stream input = File.OpenRead(media.Path))
					using (Stream output = File.Create(publicUri.Path))
					{
						input.CopyTo(output);
					}

				media.AlbumPath = publicUri.Path;

				var f = new Java.IO.File(publicUri.Path);

				try
				{
					Android.Media.MediaScannerConnection.ScanFile(context, new[] {f.AbsolutePath}, null,
						context as MediaPickerActivity);

					var values = new ContentValues();
					values.Put(definitions[media.Type]["Title"], System.IO.Path.GetFileNameWithoutExtension(f.AbsolutePath));
					values.Put(definitions[media.Type]["Description"], string.Empty);
					values.Put(definitions[media.Type]["DateTaken"], Java.Lang.JavaSystem.CurrentTimeMillis());
					values.Put(definitions[media.Type]["BucketId"], f.ToString().ToLowerInvariant().GetHashCode());
					values.Put(definitions[media.Type]["BucketDisplayName"], f.Name.ToLowerInvariant());
					values.Put("_data", f.AbsolutePath);

					var cr = context.ContentResolver;
					cr.Insert(media.Type == Abstractions.MediaType.Image
						? MediaStore.Images.Media.ExternalContentUri
						: MediaStore.Video.Media.ExternalContentUri, values);
				}
				catch (Exception ex1)
				{
					Console.WriteLine("Unable to save to scan file: " + ex1);
				}

				var contentUri = Android.Net.Uri.FromFile(f);
				var mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile, contentUri);
				context.SendBroadcast(mediaScanIntent);
			}
			catch (Exception ex2)
			{
				Console.WriteLine("Unable to save to gallery: " + ex2);
			}
		}

		public async Task<List<MediaFile>> PickMediaAsync(PickMediaOptions options = null)
		{
			if (!(await RequestCameraPermissions()))
			{
				return null;
			}

			var mediaList = await TakeMediaAsync(AllTypes, Intent.ActionPick, null);
			return await HandleMediaFiles(mediaList, options);
		}

		private readonly Context context;
		private int requestId;
		private TaskCompletionSource<List<MediaFile>> completionSource;


		async Task<bool> RequestCameraPermissions()
		{
			//We always have permission on anything lower than marshmallow.
			if ((int)Build.VERSION.SdkInt < 23)
				return true;

			bool checkCamera = HasPermissionInManifest(Android.Manifest.Permission.Camera);

			var hasStoragePermission = await Permissions.CheckStatusAsync<StoragePermission>();
			var hasCameraPermission = PermissionStatus.Granted;
			if (checkCamera)
				hasCameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();


			var permissions = new List<string>();
			var camera = nameof(Permissions.Camera);
			var storage = nameof(StoragePermission);

			if (hasCameraPermission != PermissionStatus.Granted)
				permissions.Add(camera);

			if (hasStoragePermission != PermissionStatus.Granted)
				permissions.Add(storage);

			if (permissions.Count == 0) //good to go!
				return true;

			var results = new Dictionary<string, PermissionStatus>();
			foreach (var permission in permissions)
			{
				switch (permission)
				{
					case nameof(Permissions.Camera):
						results.Add(permission, await Permissions.RequestAsync<Permissions.Camera>());
						break;
					case nameof(StoragePermission):
						results.Add(permission, await Permissions.RequestAsync<StoragePermission>());
						break;
				}
			}

			if (results.ContainsKey(storage) &&
							results[storage] != PermissionStatus.Granted)
			{
				Console.WriteLine("Storage permission Denied.");
				return false;
			}

			if (results.ContainsKey(camera) &&
							results[camera] != PermissionStatus.Granted)
			{
				Console.WriteLine("Camera permission Denied.");
				return false;
			}

			return true;
		}

		async Task<bool> RequestStoragePermission()
		{
			//We always have permission on anything lower than marshmallow.
			if ((int)Build.VERSION.SdkInt < 23)
				return true;

			var status = await Permissions.CheckStatusAsync<StoragePermission>();
			if (status != PermissionStatus.Granted)
			{
				Console.WriteLine("Does not have storage permission granted, requesting.");
				var result = await Permissions.RequestAsync<StoragePermission>();
				if (result != PermissionStatus.Granted)
				{
					Console.WriteLine("Storage permission Denied.");
					return false;
				}
			}

			return true;
		}

		IList<string> requestedPermissions;
		bool HasPermissionInManifest(string permission)
		{
			try
			{
				if (requestedPermissions != null)
					return requestedPermissions.Any(r => r.Equals(permission, StringComparison.InvariantCultureIgnoreCase));

				//try to use current activity else application context
				var permissionContext = Xamarin.Essentials.Platform.CurrentActivity ?? Android.App.Application.Context;

				if (context == null)
				{
					System.Diagnostics.Debug.WriteLine("Unable to detect current Activity or App Context. Please ensure Xamarin.Essentials is installed and initialized.");
					return false;
				}

				var info = context.PackageManager.GetPackageInfo(context.PackageName, Android.Content.PM.PackageInfoFlags.Permissions);

				if (info == null)
				{
					System.Diagnostics.Debug.WriteLine("Unable to get Package info, will not be able to determine permissions to request.");
					return false;
				}

				requestedPermissions = info.RequestedPermissions;

				if (requestedPermissions == null)
				{
					System.Diagnostics.Debug.WriteLine("There are no requested permissions, please check to ensure you have marked permissions you want to request.");
					return false;
				}

				return requestedPermissions.Any(r => r.Equals(permission, StringComparison.InvariantCultureIgnoreCase));
			}
			catch (Exception ex)
			{
				Console.Write("Unable to check manifest for permission: " + ex);
			}
			return false;
		}


		const string illegalCharacters = "[|\\?*<\":>/']";
		void VerifyOptions(StoreMediaOptions options)
		{
			if (options == null)
				throw new ArgumentNullException("options");
			if (System.IO.Path.IsPathRooted(options.Directory))
				throw new ArgumentException("options.Directory must be a relative path", "options");

			if (!string.IsNullOrWhiteSpace(options.Name))
				options.Name = Regex.Replace(options.Name, illegalCharacters, string.Empty).Replace(@"\", string.Empty);


			if (!string.IsNullOrWhiteSpace(options.Directory))
				options.Directory = Regex.Replace(options.Directory, illegalCharacters, string.Empty).Replace(@"\", string.Empty);
		}

		Intent CreateMediaIntent(int id, string type, string action, StoreMediaOptions options, bool tasked = true, bool multiple = true)
		{
			Intent pickerIntent = new Intent(this.context, typeof(MediaPickerActivity));
			pickerIntent.PutExtra(MediaPickerActivity.ExtraId, id);
			pickerIntent.PutExtra(MediaPickerActivity.ExtraType, type);
			if(type == AllTypes)
			{
				pickerIntent.PutExtra(MediaPickerActivity.ExtraMimeTypes, "video/*, image/*");
			}
			pickerIntent.PutExtra(MediaPickerActivity.ExtraAction, action);
			pickerIntent.PutExtra(MediaPickerActivity.ExtraTasked, tasked);
			pickerIntent.PutExtra(MediaPickerActivity.ExtraMultiple, multiple);
			if (options != null)
			{
				pickerIntent.PutExtra(MediaPickerActivity.ExtraPath, options.Directory);
				pickerIntent.PutExtra(MediaStore.Images.ImageColumns.Title, options.Name);

				if (options is StoreCameraMediaOptions cameraOptions)
				{
					if (cameraOptions.DefaultCamera == CameraDevice.Front)
					{
						pickerIntent.PutExtra("android.intent.extras.CAMERA_FACING", 1);
					}
					pickerIntent.PutExtra(MediaPickerActivity.ExtraSaveToAlbum, cameraOptions.SaveToAlbum);
				}
				var vidOptions = (options as StoreVideoOptions);
				if (vidOptions != null)
				{
					if (vidOptions.DefaultCamera == CameraDevice.Front)
					{
						pickerIntent.PutExtra("android.intent.extras.CAMERA_FACING", 1);
					}
					pickerIntent.PutExtra(MediaStore.ExtraDurationLimit, (int)vidOptions.DesiredLength.TotalSeconds);
					pickerIntent.PutExtra(MediaStore.ExtraVideoQuality, (int)vidOptions.Quality);
					if (vidOptions.DesiredSize != 0)
					{
						pickerIntent.PutExtra(MediaStore.ExtraSizeLimit, vidOptions.DesiredSize);
					}
				}
			}
			//pickerIntent.SetFlags(ActivityFlags.ClearTop);
			pickerIntent.SetFlags(ActivityFlags.NewTask);
			return pickerIntent;
		}

		int GetRequestId()
		{
			var id = requestId;
			if (requestId == int.MaxValue)
				requestId = 0;
			else
				requestId++;

			return id;
		}

		Task<List<MediaFile>> TakeMediaAsync(string type, string actions, StoreMediaOptions options)
		{
			int id = GetRequestId();

			var ntcs = new TaskCompletionSource<List<MediaFile>>(id);
			if (Interlocked.CompareExchange(ref completionSource, ntcs, null) != null)
				throw new InvalidOperationException("Only one operation can be active at a time");

			context.StartActivity(CreateMediaIntent(id, type, actions, options));

			EventHandler<MediaPickedEventArgs> handler = null;
			handler = (s, e) =>
			{
				var tcs = Interlocked.Exchange(ref completionSource, null);

				MediaPickerActivity.MediaPicked -= handler;

				if (e.RequestId != id)
					return;

				if (e.IsCanceled)
					tcs.SetResult(null);
				else if (e.Error != null)
					tcs.SetException(e.Error);
				else
					tcs.SetResult(e.Media);
			};

			MediaPickerActivity.MediaPicked += handler;

			return completionSource.Task;
		}

		/// <summary>
		///  Rotate an image if required and saves it back to disk.
		/// </summary>
		/// <param name="filePath">The file image path</param>
		/// <param name="mediaOptions">The options.</param>
		/// <param name="exif">original metadata</param>
		/// <returns>True if rotation or compression occured, else false</returns>
		public Task<bool> FixOrientationAndResizeAsync(string filePath, PickMediaOptions mediaOptions, ExifInterface exif)
		{
			return FixOrientationAndResizeAsync(
				filePath,
				new StoreCameraMediaOptions
				{
					PhotoSize = mediaOptions.PhotoSize,
					CompressionQuality = mediaOptions.CompressionQuality,
					CustomPhotoSize = mediaOptions.CustomPhotoSize,
					MaxWidthHeight = mediaOptions.MaxWidthHeight
				},
				exif);
		}

		/// <summary>
		///  Rotate an image if required and saves it back to disk.
		/// </summary>
		/// <param name="filePath">The file image path</param>
		/// <param name="mediaOptions">The options.</param>
		/// <param name="exif">original metadata</param>
		/// <returns>True if rotation or compression occured, else false</returns>
		public Task<bool> FixOrientationAndResizeAsync(string filePath, StoreCameraMediaOptions mediaOptions, ExifInterface exif)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return Task.FromResult(false);

			try
			{
				return Task.Run(() =>
				{
					try
					{
						var rotation = GetRotation(exif);

						// if we don't need to rotate, aren't resizing, aren't adjusting quality and don't need to check MaxWidthHeight then simply return
						if (rotation == 0 && mediaOptions.PhotoSize == PhotoSize.Full && mediaOptions.CompressionQuality == 100 && !mediaOptions.MaxWidthHeight.HasValue)
							return false;

						var percent = 1.0f;
						switch (mediaOptions.PhotoSize)
						{
							case PhotoSize.Large:
								percent = .75f;
								break;
							case PhotoSize.Medium:
								percent = .5f;
								break;
							case PhotoSize.Small:
								percent = .25f;
								break;
							case PhotoSize.Custom:
								percent = (float)mediaOptions.CustomPhotoSize / 100f;
								break;
						}

						//First decode to just get dimensions
						var options = new BitmapFactory.Options
						{
							InJustDecodeBounds = true
						};

						//already on background task
						BitmapFactory.DecodeFile(filePath, options);

						if (mediaOptions.MaxWidthHeight.HasValue)
						{
							var max = (int)(Math.Max(options.OutWidth, options.OutHeight));
							if (max > mediaOptions.MaxWidthHeight)
							{
								percent = Math.Min(percent, (float)mediaOptions.MaxWidthHeight / (float)max);
							}
							else
							{
								if (rotation == 0 && mediaOptions.PhotoSize == PhotoSize.Full && mediaOptions.CompressionQuality == 100)
									return false;
							}
						}

						var finalWidth = (int)(options.OutWidth * percent);
						var finalHeight = (int)(options.OutHeight * percent);

						//calculate sample size
						options.InSampleSize = CalculateInSampleSize(options, finalWidth, finalHeight);

						//turn off decode
						options.InJustDecodeBounds = false;


						//this now will return the requested width/height from file, so no longer need to scale
						var originalImage = BitmapFactory.DecodeFile(filePath, options);

						if (finalWidth != originalImage.Width || finalHeight != originalImage.Height)
						{
							originalImage = Bitmap.CreateScaledBitmap(originalImage, finalWidth, finalHeight, true);
						}
						if (rotation % 180 == 90)
						{
							var a = finalWidth;
							finalWidth = finalHeight;
							finalHeight = a;
						}

						//set scaled and rotated image dimensions
						exif.SetAttribute(ExifInterface.TagPixelXDimension, Java.Lang.Integer.ToString(finalWidth));
						exif.SetAttribute(ExifInterface.TagPixelYDimension, Java.Lang.Integer.ToString(finalHeight));

						//if we need to rotate then go for it.
						//then compresse it if needed
						if (rotation != 0)
						{
							var matrix = new Matrix();
							matrix.PostRotate(rotation);
							using (var rotatedImage = Bitmap.CreateBitmap(originalImage, 0, 0, originalImage.Width, originalImage.Height, matrix, true))
							{
								//always need to compress to save back to disk
								using (var stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite))
								{
									rotatedImage.Compress(Bitmap.CompressFormat.Jpeg, mediaOptions.CompressionQuality, stream);
									stream.Close();
								}
								rotatedImage.Recycle();
							}
							//change the orienation to "not rotated"
							exif.SetAttribute(ExifInterface.TagOrientation, Java.Lang.Integer.ToString(ExifInterface.OrientationNormal));

						}
						else
						{
							//always need to compress to save back to disk
							using (var stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite))
							{
								originalImage.Compress(Bitmap.CompressFormat.Jpeg, mediaOptions.CompressionQuality, stream);
								stream.Close();
							}
						}

						originalImage.Recycle();
						originalImage.Dispose();
						// Dispose of the Java side bitmap.
						GC.Collect();
						return true;

					}
					catch (Exception ex)
					{
#if DEBUG
						throw ex;
#else
                        return false;
#endif
					}
				});
			}
			catch (Exception ex)
			{
#if DEBUG
				throw ex;
#else
                return Task.FromResult(false);
#endif
			}

		}

		int CalculateInSampleSize(BitmapFactory.Options options, int reqWidth, int reqHeight)
		{
			// Raw height and width of image
			var height = options.OutHeight;
			var width = options.OutWidth;
			var inSampleSize = 1;

			if (height > reqHeight || width > reqWidth)
			{

				var halfHeight = height / 2;
				var halfWidth = width / 2;

				// Calculate the largest inSampleSize value that is a power of 2 and keeps both
				// height and width larger than the requested height and width.
				while ((halfHeight / inSampleSize) >= reqHeight
					   && (halfWidth / inSampleSize) >= reqWidth)
				{
					inSampleSize *= 2;
				}
			}

			return inSampleSize;
		}

		/// <summary>
		/// Resize Image Async
		/// </summary>
		/// <param name="filePath">The file image path</param>
		/// <param name="photoSize">Photo size to go to.</param>
		/// <param name="quality">Image quality (1-100)</param>
		/// <param name="customPhotoSize">Custom size in percent</param>
		/// <param name="exif">original metadata</param>
		/// <returns>True if rotation or compression occured, else false</returns>
		public Task<bool> ResizeAsync(string filePath, PhotoSize photoSize, int quality, int customPhotoSize, int? maxWidthHeight, ExifInterface exif)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return Task.FromResult(false);

			try
			{
				return Task.Run(() =>
				{
					try
					{
						if (photoSize == PhotoSize.Full && quality == 100 && !maxWidthHeight.HasValue)
							return false;

						var percent = 1.0f;
						switch (photoSize)
						{
							case PhotoSize.Large:
								percent = .75f;
								break;
							case PhotoSize.Medium:
								percent = .5f;
								break;
							case PhotoSize.Small:
								percent = .25f;
								break;
							case PhotoSize.Custom:
								percent = (float)customPhotoSize / 100f;
								break;
						}


						//First decode to just get dimensions
						var options = new BitmapFactory.Options
						{
							InJustDecodeBounds = true
						};

						//already on background task
						BitmapFactory.DecodeFile(filePath, options);

						if (maxWidthHeight.HasValue)
						{
							var max = (int)(Math.Max(options.OutWidth, options.OutHeight));
							if (max > maxWidthHeight)
							{
								percent = Math.Min(percent, (float)maxWidthHeight / (float)max);
							}
							else
							{
								if (photoSize == PhotoSize.Full && quality == 100)
									return false;
							}
						}

						var finalWidth = (int)(options.OutWidth * percent);
						var finalHeight = (int)(options.OutHeight * percent);

						//set scaled image dimensions
						exif.SetAttribute(ExifInterface.TagPixelXDimension, Java.Lang.Integer.ToString(finalWidth));
						exif.SetAttribute(ExifInterface.TagPixelYDimension, Java.Lang.Integer.ToString(finalHeight));

						//calculate sample size
						options.InSampleSize = CalculateInSampleSize(options, finalWidth, finalHeight);

						//turn off decode
						options.InJustDecodeBounds = false;

						//this now will return the requested width/height from file, so no longer need to scale
						using (var originalImage = BitmapFactory.DecodeFile(filePath, options))
						{

							//always need to compress to save back to disk
							using (var stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite))
							{

								originalImage.Compress(Bitmap.CompressFormat.Jpeg, quality, stream);
								stream.Close();
							}

							originalImage.Recycle();

							// Dispose of the Java side bitmap.
							GC.Collect();
							return true;
						}
					}
					catch (Exception ex)
					{
#if DEBUG
						throw ex;
#else
                        return false;
#endif
					}
				});
			}
			catch (Exception ex)
			{
#if DEBUG
				throw ex;
#else
                return Task.FromResult(false);
#endif
			}
		}

		private static float CalculatePercent(PhotoSize photoSize, int customPhotoSize)
		{
			var percent = 1.0f;
			switch (photoSize)
			{
				case PhotoSize.Large:
					percent = .75f;
					break;
				case PhotoSize.Medium:
					percent = .5f;
					break;
				case PhotoSize.Small:
					percent = .25f;
					break;
				case PhotoSize.Custom:
					percent = (float)customPhotoSize / 100f;
					break;
			}
			return percent;
		}

		void SetMissingMetadata(ExifInterface exif, Location location)
		{
			var exifPos = exif.GetLatLong();
			if (exifPos == null && location != null && location.Latitude != null && location.Longitude != null)
			{
				exif.SetLatLong((double)location.Latitude, (double)location.Longitude);
			}
			if (string.IsNullOrEmpty(exif.GetAttribute(ExifInterface.TagDatetime)))
			{
				exif.SetAttribute(ExifInterface.TagDatetime, DateTime.Now.ToString("yyyy:MM:dd hh:mm:ss"));
				exif.SetAttribute(ExifInterface.TagDatetimeOriginal, DateTime.Now.ToString("yyyy:MM:dd hh:mm:ss"));
			}
			if (string.IsNullOrEmpty(exif.GetAttribute(ExifInterface.TagMake)))
			{
				exif.SetAttribute(ExifInterface.TagMake, Build.Manufacturer);
			}
			if (string.IsNullOrEmpty(exif.GetAttribute(ExifInterface.TagModel)))
			{
				exif.SetAttribute(ExifInterface.TagModel, Build.Model);
			}
		}

		static int GetRotation(ExifInterface exif)
		{
			try
			{
				var orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, ExifInterface.OrientationNormal);

				switch (orientation)
				{
					case ExifInterface.OrientationRotate90:
						return 90;
					case ExifInterface.OrientationRotate180:
						return 180;
					case ExifInterface.OrientationRotate270:
						return 270;
					default:
						return 0;
				}
			}
			catch (Exception ex)
			{
#if DEBUG
				throw ex;
#else
                return 0;
#endif
			}
		}

		void UpdateMetadata(ref MediaFile media, ExifInterface exif)
		{
			var dateString = exif.GetAttribute(ExifInterface.TagDatetime);
			if (dateString != null)
			{
				media.MediaTakenAt = DateTime.ParseExact(dateString, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
			}
			else
			{
				media.MediaTakenAt = null;
			}
			media.Orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, ExifInterface.OrientationUndefined);
			var latString = exif.GetAttribute(ExifInterface.TagGpsLatitude);
			media.Latitude = ParseRational(latString);
			media.LatitudeRef = exif.GetAttribute(ExifInterface.TagGpsLatitudeRef);
			var longString = exif.GetAttribute(ExifInterface.TagGpsLongitude);
			media.Longitude = ParseRational(longString);
			media.LongitudeRef = exif.GetAttribute(ExifInterface.TagGpsLongitudeRef);
		}

		double? ParseRational(string rat)
		{
			double? result = null;

			try
			{
				var parts = rat.Split(',');
				var degParts = parts[0].Split('/');
				var degrees = Double.Parse(degParts[0]) / Double.Parse(degParts[1]);
				var minParts = parts[1].Split('/');
				var minutes = Double.Parse(minParts[0]) / Double.Parse(minParts[1]);
				var secParts = parts[2].Split('/');
				var seconds = Double.Parse(secParts[0]) / Double.Parse(secParts[1]);
				result = degrees + (minutes / 60) + (seconds / 3600);
			}
			catch (Exception)
			{
			}

			return result;
		}

		public Task<MediaFile> TakeMediaAsync(StoreVideoOptions options)
		{
			throw new NotImplementedException();
		}

		public bool RemoveMediaFromGallery(string[] ids) => throw new NotImplementedException();
	}
}
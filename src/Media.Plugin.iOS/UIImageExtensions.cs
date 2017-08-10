using CoreGraphics;
using System;
using System.Drawing;
using UIKit;
using CoreImage;

namespace Plugin.Media
{
    /// <summary>
    /// Static mathods for UIImage
    /// </summary>
    public static class UIImageExtensions
    {
        public static UIImage ResizeImageWithAspectRatio(this UIImage imageSource, float scale)
        {
            if (scale > 1.0f)
                return imageSource;
			
            using (CIContext c = CIContext.Create())
            {
                var sourceImage = CIImage.FromCGImage(imageSource.CGImage);

                var f = new CILanczosScaleTransform
                {
                    Scale = scale,
                    Image = sourceImage,
                    AspectRatio = 1.0f
                };


                var output = f.OutputImage;

                var cgi = c.CreateCGImage(output, output.Extent);
                return UIImage.FromImage(cgi, 1.0f, imageSource.Orientation);
            }
        }

        /// <summary>
        /// Resize image to maximum size
        /// keeping the aspect ratio
        /// </summary>
        public static UIImage ResizeImageWithAspectRatio(this UIImage sourceImage, float maxWidth, float maxHeight)
        {
			

            var sourceSize = sourceImage.Size;
            var maxResizeFactor = Math.Max(maxWidth / sourceSize.Width, maxHeight / sourceSize.Height);
            if (maxResizeFactor > 1) 
                return sourceImage;
            var width = maxResizeFactor * sourceSize.Width;
            var height = maxResizeFactor * sourceSize.Height;
            UIGraphics.BeginImageContext(new CGSize(width, height));
            sourceImage.Draw(new CGRect(0, 0, width, height));
            var resultImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return resultImage;
        }

        /// <summary>
        /// Resize image, but ignore the aspect ratio
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static UIImage ResizeImage(this UIImage sourceImage, float width, float height)
        {
            UIGraphics.BeginImageContext(new SizeF(width, height));
            sourceImage.Draw(new RectangleF(0, 0, width, height));
            var resultImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return resultImage;
        }

        /// <summary>
        /// Crop image to specitic size and at specific coordinates
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="crop_x"></param>
        /// <param name="crop_y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static UIImage CropImage(this UIImage sourceImage, int crop_x, int crop_y, int width, int height)
        {
            var imgSize = sourceImage.Size;
            UIGraphics.BeginImageContext(new SizeF(width, height));
            var context = UIGraphics.GetCurrentContext();
            var clippedRect = new RectangleF(0, 0, width, height);
            context.ClipToRect(clippedRect);
            var drawRect = new CGRect(-crop_x, -crop_y, imgSize.Width, imgSize.Height);
            sourceImage.Draw(drawRect);
            var modifiedImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return modifiedImage;
        }
        
        public static UIImage ScaleImage(this UIImage sourceImage, float scale)
        {
            if (scale >= 0.999f)
                return sourceImage;

            UIImage resultImage;
            float width = (float)(sourceImage.Size.Width * scale);
            float height = (float)(sourceImage.Size.Height * scale);

            if (sourceImage.Orientation == UIImageOrientation.Left || sourceImage.Orientation == UIImageOrientation.Right 
                || sourceImage.Orientation == UIImageOrientation.LeftMirrored || sourceImage.Orientation == UIImageOrientation.RightMirrored) {
                var w = width;
                width = height;
                height = w;
            }
            using (CGImage image = sourceImage.CGImage)
            {
                CGImageAlphaInfo alpha = image.AlphaInfo == CGImageAlphaInfo.None ? CGImageAlphaInfo.NoneSkipLast : image.AlphaInfo;
                CGColorSpace color = CGColorSpace.CreateDeviceRGB();
                var bitmap = new CGBitmapContext(IntPtr.Zero, (int)width, (int)height, image.BitsPerComponent, image.BytesPerRow, color, alpha);
                bitmap.DrawImage(new CGRect(0, 0, (int)width, (int)height), image);
                resultImage = UIImage.FromImage(bitmap.ToImage(), 1.0f, sourceImage.Orientation);
            }

            return resultImage;
        }

// method taken from xamarin forum
// https://forums.xamarin.com/discussion/19778/uiimage-rotation-and-transformation
//@author: Fazri Zubair
        public static UIImage RotateImage(this UIImage imageIn) {
            int kMaxResolution = 8192;

            CGImage imgRef = imageIn.CGImage;
            float width = imgRef.Width;
            float height = imgRef.Height;
            CGAffineTransform transform = CGAffineTransform.MakeIdentity ();
            RectangleF bounds = new RectangleF( 0, 0, width, height );

            if ( width > kMaxResolution || height > kMaxResolution )
            {
                float ratio = width/height;

                if (ratio > 1)
                {
                    bounds.Width  = kMaxResolution;
                    bounds.Height = bounds.Width / ratio;
                }
                else
                {
                    bounds.Height = kMaxResolution;
                    bounds.Width  = bounds.Height * ratio;
                }
            }

            float scaleRatio = bounds.Width / width;
            SizeF imageSize = new SizeF( width, height);
            float boundHeight;

            switch(imageIn.Orientation)
            {
                case UIImageOrientation.Up:                                        //EXIF = 1
                    transform = CGAffineTransform.MakeIdentity();
                    break;

                case UIImageOrientation.UpMirrored:                                //EXIF = 2
                    transform = CGAffineTransform.MakeTranslation (imageSize.Width, 0f);
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    break;

                case UIImageOrientation.Down:                                      //EXIF = 3
                    transform = CGAffineTransform.MakeTranslation (imageSize.Width, imageSize.Height);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI);
                    break;

                case UIImageOrientation.DownMirrored:                              //EXIF = 4
                    transform = CGAffineTransform.MakeTranslation (0f, imageSize.Height);
                    transform = CGAffineTransform.MakeScale(1.0f, -1.0f);
                    break;

                case UIImageOrientation.LeftMirrored:                              //EXIF = 5
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation (imageSize.Height, imageSize.Width);
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    transform = CGAffineTransform.Rotate(transform, 3.0f * (float)Math.PI * 0.5f);
                    break;

                case UIImageOrientation.Left:                                      //EXIF = 6
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation (0.0f, imageSize.Width);
                    transform = CGAffineTransform.Rotate(transform, 3.0f * (float)Math.PI  * 0.5f);
                    break;

                case UIImageOrientation.RightMirrored:                             //EXIF = 7
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI  * 0.5f);
                    break;

                case UIImageOrientation.Right:                                     //EXIF = 8
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation(imageSize.Height, 0.0f);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI   * 0.5f);
                    break;

                default:
                    throw new Exception("Invalid image orientation");
            }

            UIGraphics.BeginImageContext(bounds.Size);

            CGContext context = UIGraphics.GetCurrentContext ();

            if ( imageIn.Orientation == UIImageOrientation.Right || imageIn.Orientation == UIImageOrientation.Left )
            {
                context.ScaleCTM(-scaleRatio, scaleRatio);
                context.TranslateCTM(-height, 0);
            }
            else
            {
                context.ScaleCTM(scaleRatio, -scaleRatio);
                context.TranslateCTM(0, -height);
            }

            context.ConcatCTM(transform);
            context.DrawImage (new RectangleF (0, 0, width, height), imgRef);

            UIImage imageCopy = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();

            return imageCopy;
        }
    }
}
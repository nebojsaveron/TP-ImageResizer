using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Web;
using NLog;

namespace ImageResizer
{
    /// <summary>
    /// Request Template:
    ///     ~/usermedia/[GUID]/w[width-in-pixels]-h[height-in-pixels].png
    ///     ~/usermedia/[GUID]/w[width-in-pixels].png
    ///     ~/usermedia/[GUID]/h[height-in-pixels].png
    ///     ~/usermedia/[GUID]/cropped-w[width-in-pixels]-h[height-in-pixels].png
    ///     ~/usermedia/[GUID]/original.png
    /// For Example, with GUID: "acypAlENrUeSOX4-1n-yzg"
    ///     ~/usermedia/acypAlENrUeSOX4-1n-yzg/w40-h60.png
    /// 
    /// </summary>
    public class ImageStoreHandler : IHttpHandler
    {
        private static Logger Log = LogManager.GetCurrentClassLogger();

        private static string _rootImagePath;
        private static string RootImageRelativePath
        {
            get { return _rootImagePath ?? (_rootImagePath = SiteConfig.Instance.UserImagesRelativePath); }
        }

        private static string _sharedImagePath;
        private static string SharedImagePath
        {
            get { return _sharedImagePath ?? (_sharedImagePath = SiteConfig.Instance.UserImagesSharedPath); }
        }

        #region IHttpHandler Members

        public bool IsReusable { get { return false; } }

        //todo might want to go through the logic. might be a way to make it cleaner
        public void ProcessRequest(HttpContext context)
        {
            //see if image has been created, if so - serve and end processing.
            string physicalPath = context.Request.PhysicalPath;
            if (File.Exists(physicalPath))
            {
                using (var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var image = Image.FromStream(stream))
                    {
                        ServeImage(context, image);
                    }
                }

            }
            else
            {

                //string guid = "acypAlENrUeSOX4-1n-yzg";
                Image image;

                //path = "<guid>/<filename>.png"
                //args = ["<guid>","w<width>-h<height>",".png"];
                string[] args = context.Request.AppRelativeCurrentExecutionFilePath
                                .Replace(RootImageRelativePath, string.Empty)
                                .Split('/', '.');

                if (args.Length != 3)
                {
                    //incorrect file structure, return not found
                    ReturnNotFound(context);
                    return;
                }

                string guid = args[0];
                string sizeParam = args[1];

                Log.Info("About to retrieve image " + guid);
                try
                {
                    image = RetreiveImage(sizeParam, guid);

                    if (image == null)
                    {
                        Log.Info("Image was null...");
                        ReturnNotFound(context);
                        return;
                    }

                    ServeImage(context, image);
                }
                catch (Exception e)
                {
                    Log.ErrorException("Catch All Image Error", e);

                    ReturnNotFound(context);
                }
            }
        }

        #endregion

        private struct SizeParam
        {
            public int Width;
            public int Height;
            public bool IsCropped;
        }

        private static SizeParam GetWidthHeightParams(string sizeParam)
        {
            var args = sizeParam.Split('-');
            int width = 0, height = 0;
            bool isCropped = false;
            foreach (var s in args)
            {
                switch (s[0])
                {
                    case 'w':
                        int.TryParse(s.Substring(1), out width);
                        break;
                    case 'h':
                        int.TryParse(s.Substring(1), out height);
                        break;
                    case 'c':
                        isCropped = s.Equals("cropped", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
            return new SizeParam
            {
                Width = width,
                Height = height,
                IsCropped = isCropped
            };
        }

        private Image RetreiveImage(string sizeParam, string guid)
        {
            //check if file exists
            //if exists, return file
            //if not exists, make image
            //save image
            //return image

            //check existence of original. if not, try to grab from shared folder
            if (!File.Exists(OrigFilePath(guid)))
            {
                //check if file share has it
                var sharedpath = HttpContext.Current.Server.MapPath(SharedImagePath + guid + ".png");
                if (File.Exists(sharedpath))
                {

                    RetrieveSharedOriginalImage(guid, sharedpath);
                }
                else
                {
                    return null;
                }
            }

            if (sizeParam == "original")
            {
                return Image.FromFile(OrigFilePath(guid));
                //todo if we use a stream, it won't work for some resaon
                //using (var stream = new FileStream(OrigFilePath(guid), FileMode.Open, FileAccess.Read))
                //{
                //    using (var image = (Bitmap)Image.FromStream(stream))
                //    {
                //        return image;
                //    }
                //}
            }

            var size = GetWidthHeightParams(sizeParam);

            //image = RetreiveImage(guid, size.Width, size.Height, size.IsCropped);

            string filePathToReturn = FilePath(guid, size.Width, size.Height, size.IsCropped);
            //if it existed, we wouldn't be in this method
            //if (File.Exists(filePathToReturn))
            //{
            //    using (var stream = new FileStream(filePathToReturn, FileMode.Open, FileAccess.Read))
            //    {
            //        using (var image = Image.FromStream(stream))
            //        {
            //            return image;
            //        }
            //    }
            //}

            string originalImageFilePath = OrigFilePath(guid);

            if (File.Exists(originalImageFilePath))
            {
                //here we want to look at how many images have been created recently...  and see if it is maybe too many.

                if (!HttpContext.Current.Request.IsAuthenticated)
                {
                    //if user is authenticated, go ahead and let them resize. otherwise, make check...

                    var folder = Path.Combine(HttpContext.Current.Server.MapPath(RootImageRelativePath), guid);
                    var olderThan = DateTime.UtcNow.AddSeconds(-10);

                    var tooMany = Directory.EnumerateFiles(folder)
                                     .Select(File.GetCreationTimeUtc)
                                     .Where(dt => dt > olderThan) // files created in last 10 seconds
                                     .Skip(5)
                                     .Any(); // are there more than 5??

                    if (tooMany)
                    {
                        return null;
                    }
                }





                using (var stream = new FileStream(originalImageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var image = (Bitmap)Image.FromStream(stream))
                    {

                        // var image = (Bitmap) Image.FromFile(originalImageFilePath);

                        var resizedImage = ResizeImage(image, size.Width, size.Height, size.IsCropped);

                        resizedImage.Save(filePathToReturn, ImageFormat.Png);

                        return resizedImage;
                    }
                }
            }
            return null;
        }
        private void RetrieveSharedOriginalImage(string guid, string originalImageFilePath)
        {
            string rootPath = HttpContext.Current.Server.MapPath(RootImageRelativePath);
            var folderPath = Path.Combine(rootPath, guid);
            var imagePath = Path.Combine(folderPath, "original.png");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            using (var stream = new FileStream(originalImageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var image = (Bitmap)Image.FromStream(stream))
                {
                    //var image = (Bitmap) Image.FromFile(originalImageFilePath);

                    image.Save(imagePath, ImageFormat.Png);
                    //return image;
                }
            }
        }

        private static Image ResizeImage(Image image, int width, int height, bool isCropped)
        {
            return isCropped ?
                ResizeWithCropping(image, width, height) :
                ResizeWithoutCropping(image, width, height);
        }

        private static Bitmap ResizeWithoutCropping(Image image, int width, int height)
        {
            int sourceWidth = image.Width;
            int sourceHeight = image.Height;

            var currentRatio = ((float)sourceWidth / (float)sourceHeight);
            if (height == 0 && width > 0)
            {
                //resize to width
                height = (int)(width / currentRatio);
            }
            if (width == 0 && height > 0)
            {
                //resize to height
                width = (int)(height * currentRatio);
            }

            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)width / (float)sourceWidth);
            nPercentH = ((float)height / (float)sourceHeight);
            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
                destX = Convert.ToInt16((width -
                              (sourceWidth * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentW;
                destY = Convert.ToInt16((height -
                              (sourceHeight * nPercent)) / 2);
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            var bmPhoto = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var grPhoto = Graphics.FromImage(bmPhoto))
            {
                grPhoto.Clear(Color.White);

                grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
                grPhoto.SmoothingMode = SmoothingMode.HighQuality;
                grPhoto.PixelOffsetMode = PixelOffsetMode.HighQuality;
                grPhoto.DrawImage(image,
                    new Rectangle(destX, destY, destWidth, destHeight),
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    GraphicsUnit.Pixel);
            }
            //bmPhoto.MakeTransparent(Color.White);
            return bmPhoto;
        }

        private static Bitmap ResizeWithCropping(Image image, int width, int height)
        {
            int sourceWidth = image.Width;
            int sourceHeight = image.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)width / (float)sourceWidth);
            nPercentH = ((float)height / (float)sourceHeight);

            if (nPercentH < nPercentW)
            {
                nPercent = nPercentW;
                destY = (int)((height - (sourceHeight * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentH;
                destX = (int)((width - (sourceWidth * nPercent)) / 2);
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap bmPhoto = new Bitmap(width,
                    height, PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(image.HorizontalResolution,
                    image.VerticalResolution);

            using (var grPhoto = Graphics.FromImage(bmPhoto))
            {
                grPhoto.Clear(Color.White);

                grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
                grPhoto.SmoothingMode = SmoothingMode.HighQuality;
                grPhoto.PixelOffsetMode = PixelOffsetMode.HighQuality;

                grPhoto.DrawImage(image,
                    new Rectangle(destX, destY, destWidth, destHeight),
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    GraphicsUnit.Pixel);
            }
            //bmPhoto.MakeTransparent(Color.White);
            return bmPhoto;
        }

        private static string FilePath(string guid, int width, int height, bool isCropped = false)
        {
            var RootImageFolder = HttpContext.Current.Server.MapPath(RootImageRelativePath);
            if (width == 0 && height == 0)
            {
                return string.Format(@"{0}\{1}\original.png", RootImageFolder, guid);
            }
            if (width == 0 && height > 0)
            {
                return string.Format(@"{0}\{1}\h{2}.png", RootImageFolder, guid, height);
            }
            if (height == 0 && width > 0)
            {
                return string.Format(@"{0}\{1}\w{2}.png", RootImageFolder, guid, width);
            }
            return string.Format(@"{0}\{1}\{4}w{2}-h{3}.png", RootImageFolder, guid, width, height, isCropped ? "cropped-" : "");
        }

        private static string OrigFilePath(string guid)
        {
            return FilePath(guid, 0, 0);
        }

        private static void ServeImage(HttpContext context, Image image)
        {
            context.Response.Clear();
            context.Response.ContentType = "image/png";
            context.Response.CacheControl = "public";
            context.Response.Expires = 525600; //one year
            context.Response.AddHeader("content-disposition", "inline;");

            image.Save(context.Response.OutputStream, ImageFormat.Png);
            image.Dispose();
        }

        private static void ReturnNotFound(HttpContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }
}
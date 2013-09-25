using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

using ImageResizer.Configuration;

namespace TestWeb
{
    public class ImageConfig
    {
        /// <summary>
        /// Configures the ImageResizer pipeline to intercept all requests for images and get resize parameters out of the request Url.
        /// </summary>
        /// <param name="urlRegex">Regular expression to parse the request Url</param>
        public static void ConfigureImageResizer(Regex urlRegex)
        {
            Config.Current.Pipeline.Rewrite += delegate(IHttpModule sender, HttpContext context, IUrlEventArgs ev)
            {
                if (ev.VirtualPath.StartsWith(VirtualPathUtility.ToAbsolute(SiteConfig.Instance.UserImagesRelativePath), StringComparison.OrdinalIgnoreCase))
                {
                    // Match the given regex with current request Url
                    var match = urlRegex.Match(ev.VirtualPath);

                    if (match.Success)
                    {
                        string format;
                        var guid = match.Groups[1].ToString();

                        // Set size, crop and format parameters for ImageResizer
                        if (match.Groups[3].Value == "original")
                        {
                            format = match.Groups[4].Value;
                        }
                        else
                        {
                            format = match.Groups[10].Value;

                            if (!string.IsNullOrEmpty(match.Groups[5].Value)) { ev.QueryString["crop"] = "auto"; }
                            if (!string.IsNullOrEmpty(match.Groups[7].Value)) { ev.QueryString["width"] = match.Groups[7].Value; }
                            if (!string.IsNullOrEmpty(match.Groups[9].Value)) { ev.QueryString["height"] = match.Groups[9].Value; }
                        }

                        ev.QueryString["format"] = format;

                        // Set the real Url to get the image 
                        var filename = String.Format("{0}.{1}", guid, format);
                        var imageUrl = Path.Combine(SiteConfig.Instance.UserImagesSharedPath, filename);

                        ev.VirtualPath = imageUrl;
                    }
                }
            };
        }
    }
}
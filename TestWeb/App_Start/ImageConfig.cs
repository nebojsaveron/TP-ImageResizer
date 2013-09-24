using System;
using System.Text.RegularExpressions;
using System.Web;

using ImageResizer.Configuration;

namespace TestWeb
{
    public class ImageConfig
    {
        public static void ConfigureImageResizer(Regex regex)
        {
            Config.Current.Pipeline.Rewrite += delegate(IHttpModule sender, HttpContext context, IUrlEventArgs ev)
            {
                if (ev.VirtualPath.StartsWith(VirtualPathUtility.ToAbsolute(SiteConfig.Instance.UserImagesRelativePath), StringComparison.OrdinalIgnoreCase))
                {
                    var match = regex.Match(ev.VirtualPath);

                    if (match.Success)
                    {
                        string format;
                        var guid = match.Groups[1].ToString();

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

                        var filename = String.Format("{0}.{1}", guid, format);
                        ev.VirtualPath = String.Format(SiteConfig.Instance.UserImagesSharedPath + filename);
                    }
                }
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace ImageResizer
{
    public class SiteConfig : ConfigurationSection
    {
        private static SiteConfig _instance;

        public static SiteConfig Instance
        {
            get
            {
                return _instance ??
                       (_instance = (SiteConfig)ConfigurationManager.GetSection("SiteConfig"));
            }
        }

        [ConfigurationProperty("UserImagesRelativePath", DefaultValue = "")]
        public string UserImagesRelativePath
        {
            get { return (string)this["UserImagesRelativePath"]; }
            set { this["UserImagesRelativePath"] = value; }
        }

        [ConfigurationProperty("UserImagesSharedPath", DefaultValue = "")]
        public string UserImagesSharedPath
        {
            get { return (string)this["UserImagesSharedPath"]; }
            set { this["UserImagesSharedPath"] = value; }
        }

    }
}
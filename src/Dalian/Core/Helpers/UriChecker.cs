using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Dalian.Core.Helpers
{
    public class UriChecker
    {
        public static bool IsValidURI(string uri)
        {
            if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute)) {
                return false;
            }

            return true;
            //return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
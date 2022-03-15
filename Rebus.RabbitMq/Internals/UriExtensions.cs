using System;
using System.Net;
using System.Web;

namespace Rebus.Internals;

static class UriExtensions
{
    public static bool TryGetCredentials(this Uri uri, out NetworkCredential result)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        var userInfo = uri.UserInfo ?? "";
        var parts = userInfo.Split(':');

        if (parts.Length != 2)
        {
            result = null;
            return false;
        }

        try
        {
            var username = HttpUtility.UrlDecode(parts[0]);
            var password = HttpUtility.UrlDecode(parts[1]);

            result = new NetworkCredential(username, password);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
}
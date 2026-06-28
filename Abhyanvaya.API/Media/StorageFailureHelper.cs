using Amazon.S3;
using System.Net.Http;

namespace Abhyanvaya.API.Media;

internal static class StorageFailureHelper
{
    public static bool IsStorageOrNetworkFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is AmazonS3Exception || e is HttpRequestException)
                return true;
            if (e is System.Net.Sockets.SocketException)
                return true;
        }

        return false;
    }
}

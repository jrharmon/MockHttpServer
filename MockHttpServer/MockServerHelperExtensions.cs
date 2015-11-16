using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public static class MockServerHelperExtensions
    {
        private static readonly Dictionary<HttpListenerRequest, string> RequestContent = new Dictionary<HttpListenerRequest, string>();

        public static string Content(this HttpListenerRequest request)
        {
            if (RequestContent.ContainsKey(request))
                return RequestContent[request];

            var buffer = new byte[request.ContentLength64];
            var data = request.InputStream.Read(buffer, 0, buffer.Length);
            RequestContent[request] = Encoding.UTF8.GetString(buffer);

            return RequestContent[request];
        }

        //after a request has been processed, it can remove itself from internal memory here
        public static void ClearContent(this HttpListenerRequest request)
        {
            if (RequestContent.ContainsKey(request))
                RequestContent.Remove(request);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public static class MockHttpServerHelperExtensions
    {
        private static Dictionary<HttpListenerRequest, string> requestContent = new Dictionary<HttpListenerRequest, string>();

        public static string Content(this HttpListenerRequest request)
        {
            if (requestContent.ContainsKey(request))
                return requestContent[request];

            var buffer = new byte[request.ContentLength64];
            var data = request.InputStream.Read(buffer, 0, buffer.Length);
            requestContent[request] = Encoding.UTF8.GetString(buffer);

            return requestContent[request];
        }

        //after a request has been processed, it can remove itself from internal memory here
        public static void ClearContent(this HttpListenerRequest request)
        {
            if (requestContent.ContainsKey(request))
                requestContent.Remove(request);
        }
    }
}

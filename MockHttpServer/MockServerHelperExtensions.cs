using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public static class MockServerHelperExtensions
    {
        #region HttpListenerRequest

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
        internal static void ClearContent(this HttpListenerRequest request)
        {
            if (RequestContent.ContainsKey(request))
                RequestContent.Remove(request);
        }

        #endregion HttpListenerRequest

        #region HttpListenerResponse

        public static void Content(this HttpListenerResponse response, byte[] buffer)
        {
            response.ContentLength64 += buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public static void Content(this HttpListenerResponse response, Stream stream)
        {
            response.ContentLength64 += stream.Length;
            stream.CopyTo(response.OutputStream);
        }

        public static void Content(this HttpListenerResponse response, string value)
        {
            var buffer = Encoding.UTF8.GetBytes(value);
            response.ContentLength64 += buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public static HttpListenerResponse ContentType(this HttpListenerResponse response, string contentType)
        {
            response.ContentType = contentType;

            return response;
        }

        public static HttpListenerResponse Cookie(this HttpListenerResponse response, string name, string value, string path = null, string domain = null)
        {
            if (path == null)
                response.AppendCookie(new Cookie(name, value));
            else if (domain == null)
                response.AppendCookie(new Cookie(name, value, path));
            else
                response.AppendCookie(new Cookie(name, value, path, domain));

            return response;
        }

        public static HttpListenerResponse Header(this HttpListenerResponse response, string name, string value)
        {
            response.AppendHeader(name, value);

            return response;
        }

        public static void JsonTextContent(this HttpListenerResponse response, string value)
        {
            response.ContentType("application/json");
            var buffer = Encoding.UTF8.GetBytes(value);
            response.ContentLength64 += buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public static HttpListenerResponse StatusCode(this HttpListenerResponse response, int statusCode)
        {
            response.StatusCode = statusCode;

            return response;
        }

        #endregion HttpListenerResponse
    }
}

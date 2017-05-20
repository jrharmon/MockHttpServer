using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public class MockServer : IDisposable
    {
        private HttpListener _listener;
        private List<MockHttpHandler> _requestHandlers;
        private readonly object _requestHandlersLock = new object();
        private readonly Action<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>> _preHandler; //if set, this will be executed for every request before the handler is called
        private readonly string _hostName; //the hostname to listen on.  defaults to localhost, but if you run as admin, you can use * or + as wild cards.  if a port is registered by netsh with a * or +, you can specify it in the constructor to use the wildcard without admin rights

        public IReadOnlyList<MockHttpHandler> RequestHandlers => _requestHandlers;

        public int Port { get; }

        public MockServer(int port, string url, Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, string> handlerFunction, string hostName = "localhost")
            : this(port, new List<MockHttpHandler> { new MockHttpHandler(url, handlerFunction) }, hostName)
        {

        }

        public MockServer(int port, string url, Action<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>> handlerAction, string hostName = "localhost")
            : this(port, new List<MockHttpHandler> { new MockHttpHandler(url, handlerAction) }, hostName)
        {

        }

        public MockServer(int port, IEnumerable<MockHttpHandler> requestHandlers, string hostName = "localhost")
            : this(port, requestHandlers, null, hostName)
        {

        }

        public MockServer(int port, IEnumerable<MockHttpHandler> requestHandlers, Action<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>> preHandler, string hostName = "localhost")
        {
            _requestHandlers = requestHandlers.ToList(); //make a copy of the items, in case they get cleared later
            _preHandler = preHandler;
            _hostName = hostName;

            Port = port > 0 ? port : GetRandomUnusedPort();

            //create and start listener
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_hostName}:{Port}/");
            _listener.Start();
            HandleRequests();
        }

        #region Private Methods

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task HandleRequests()
        {
            try
            {
                //listen for all requests
                while (_listener.IsListening)
                {
                    //get the request
                    var context = await _listener.GetContextAsync();

                    try
                    {
                        //determine the hanlder
                        Dictionary<string, string> parameters = null;
                        MockHttpHandler handler;
                        lock (_requestHandlersLock)
                        {
                            handler = _requestHandlers.FirstOrDefault(h => h.MatchesUrl(context.Request.RawUrl, context.Request.HttpMethod, out parameters));
                        }

                        //run the shared pre-handler
                        _preHandler?.Invoke(context.Request, context.Response, parameters ?? new Dictionary<string, string>());

                        //get the response string
                        string responseString = null;
                        if (handler != null)
                        {
                            //add the query string parameters to the pre-defined url parameters that were set from MatchesUrl()
                            foreach (var qsParamName in context.Request.QueryString.AllKeys)
                                parameters[qsParamName] = context.Request.QueryString[qsParamName];

                            try
                            {
                                if (handler.HandlerFunction != null)
                                    responseString = handler.HandlerFunction(context.Request, context.Response, parameters);
                                else
                                    handler.HandlerAction(context.Request, context.Response, parameters);
                            }
                            catch (Exception ex)
                            {
                                responseString = $"Exception in handler: {ex.Message}";
                                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            context.Response.ContentType("text/plain").StatusCode(404);
                            responseString = "No handler provided for URL: " + context.Request.RawUrl;
                        }
                        context.Request.ClearContent();

                        //send the response, if there is not (if responseString is null, then the handler method should have manually set the output stream)
                        if (responseString != null)
                        {
                            var buffer = Encoding.UTF8.GetBytes(responseString);
                            context.Response.ContentLength64 += buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    finally
                    {
                        context.Response.OutputStream.Close();
                        context.Response.Close();
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                //when the listener is stopped, it will throw an exception for being cancelled, so just ignore it
                if (ex.Message != "The I/O operation has been aborted because of either a thread exit or an application request")
                    throw;
            }
        }

        #endregion Private Methods

        #region Public Methods

        public void AddRequestHandler(MockHttpHandler requestHandler)
        {
            lock (_requestHandlersLock)
            {
                _requestHandlers.Add(requestHandler);
            }
        }

        public void ClearRequestHandlers()
        {
            lock (_requestHandlersLock)
            {
                _requestHandlers.Clear();
            }
        }

        public void SetRequestHandlers(IEnumerable<MockHttpHandler> requestHandlers)
        {
            lock (_requestHandlersLock)
            {
                _requestHandlers = requestHandlers.ToList();
            }
        }

        #endregion Public Methods

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && (_listener?.IsListening ?? false))
            {
                _listener.Stop();
            }
        }

        #endregion IDisposable
    }
}

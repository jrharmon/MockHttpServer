# MockHttpServer
A library to help with unit/acceptance tests of code that relies on an external HTTP service, by allowing you to easily mock one.

##Usage

In its simplest form, when creating an instance of MockServer, you just need to specify the port to
listen on, a url to listen
for (just the part after the port), and a lambda that returns a string.

MockServer implements IDisposable, so always be sure to dispose of it when done, or wrap it in a using
statement.  Otherwise, it will continue waiting for a request, and will cause any new instances to fail,
due to a conflict.

``` C#
using (new MockServer(TestPort, "", (req, rsp, parms) => "Result Body"))
{
    var result = client.Execute(new RestRequest("", Method.GET));
}
```

By default, it will accept any HTTP verb at the specified url, and can only handle that one type of request.

The second type of constructor takes in a list of MockHttpListener objects, allowing more control over the configuration,
as well as the ability to specify any number of requests to handle.

The following example shows two different HTTP verbs being configured for the same url.  Once you specify an explicit
verb for a URL, only that verb will be accepted, so in this case, if you tried calling that URL with a DELETE verb,
you would get the message: "No handler provided for URL: /data"

``` C#
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/data", "GET", (req, rsp, parms) => "Get"),
    new MockHttpHandler("/data", "POST", (req, rsp, parms) => "Post")
};

using (new MockServer(TestPort, requestHandlers))
{
    var result = client.Execute(new RestRequest("data", Method.GET));
    result = client.Execute(new RestRequest("data", Method.POST));
    result = client.Execute(new RestRequest("data", Method.DELETE)); //does not work
}
```

You can return more than just strings as well.  The following example manually sets the output to a stream.  It is
still just returning plain text, but the stream could be any array of bytes, such as an image or file.

Notice that it returns null, which tells MockServer to not set the output itself, as it is being taken care of. 

``` C#
using (new MockServer(TestPort, "/api", (req, rsp, parms) =>
{
    var buffer = Encoding.UTF8.GetBytes(expectedResult);
    rsp.ContentLength64 = buffer.Length;
    rsp.OutputStream.Write(buffer, 0, buffer.Length);
    rsp.OutputStream.Close();
    return null;
}))
{
    var result = client.Execute(new RestRequest("/api", Method.GET));
}
```

Notice that the lambda for the handlers takes in both the request (HttpListenerRequest) and response (HttpListenerResponse)
objects, which allows you full access to additional input information, and the ability to set adiditional output information.

You can read/write headers, cookies, etc, and also set the status code of the response. 

``` C#
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/succeed", (req, rsp, parms) => "succeed"),
    new MockHttpHandler("/fail", (req, rsp, parms) => 
    {
        rsp.StatusCode = (int)HttpStatusCode.InternalServerError;
        return "fail";
    })
};

using (new MockServer(TestPort, requestHandlers))
{
    var result = client.Execute(new RestRequest("/succeed", Method.GET));
    result = client.Execute(new RestRequest("/fail", Method.GET));
}
```

##Parameters

There is built-in support for easily accessing parameters from the query string, or the URL itself.  The third
parameter of the handler lambda is a dictionary of all parameters from the URL and query string combined.  Parameters
in the URL are defined by placing a label inside of {}.  That will act as a wild-card, and store the value along with
the label used.

Helow is an example of using query string parameters.

``` C#
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/person?active=true", (req, rsp, parms) => "Active"),
    new MockHttpHandler("/person?active=false", (req, rsp, parms) => "Not Active"),
    new MockHttpHandler("/person", (req, rsp, parms) => $"{parms["person_id"]}, {parms["age"]}")
};

using (new MockServer(TestPort, requestHandlers))
{
    var result = client.Execute(new RestRequest("/person?person_id=123&age=82", Method.POST)); //"123, 82"
    result = client.Execute(new RestRequest("/person?active=true", Method.POST)); //"ACTIVE"
    result = client.Execute(new RestRequest("/person?active=false", Method.POST)); //"Not Active"
}
```

And here is an example of accessing parameters from within the URL.

``` C#
using (new MockServer(TestPort, "xml/{category}/{id}", (req, rsp, parms) =>
{
    rsp.Headers.Add("Content-Type", "application/xml; charset=utf-8");
    return $"<Value>{parms["category"]} - {parms["id"]}</Value>";
}))
{
    var result = client.Execute(new RestRequest("xml/horror/12345/", Method.POST)); //"<Value>horror - 12345</Value>"
}
```
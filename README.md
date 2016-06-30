# MockHttpServer

[![Build status](https://ci.appveyor.com/api/projects/status/numsjbmqdxpercff?svg=true)](https://ci.appveyor.com/project/jrharmon/mockhttpserver)
[![NuGet Version](http://img.shields.io/nuget/v/MockHttpServer.svg?style=flat)](https://www.nuget.org/packages/MockHttpServer/)

A library to help with unit/acceptance tests of code that relies on an external HTTP service, by allowing you to
easily mock one.

It can easily be installed through NuGet, using the [MockHttpServer](http://nuget.org/packages/MockHttpServer) package.

## Table of Contents

  1. [Usage](#usage)
  1. [Multiple Handlers](#multiple-handlers)
  1. [Admin Requirements](#admin-requirements)
  1. [Detailed Input/Output](#detailed-inputoutput)
  1. [Extension Methods](#extension-methods)
  1. [Custom Extension Methods](#custom-extension-methods)
  1. [Parameters](#parameters)

##Usage

In its simplest form, when creating an instance of MockServer, you just need to specify the port to
listen on, a url to listen for (just the part after the port), and a lambda that returns a string
or void.

MockServer implements IDisposable, so always be sure to dispose of it when done, or wrap it in a using
statement.  Otherwise, it will continue waiting for a request, and will cause any new instances to fail,
due to a conflict.

``` C#
var client = new RestClient($"http://localhost:{TestPort}/");
using (new MockServer(TestPort, "", (req, rsp, prm) => "Result Body"))
{
    var result = client.Execute(new RestRequest("", Method.GET));
}
```

By default, it will accept any HTTP verb at the specified url, and can only handle that one type of request.

If you don't have a specific port you need to listen on, but want to make sure you aren't going to conflict with another
program listening on a port (perhaps your build server is testing two instances of your application at once), then you
can set the port to 0 when creating MockServer, and it will pick a random un-used port.  You can then use the Port
property to see what was used, and make requests against that port.  (Internally, TcpListener is used to find the port,
which may trigger a firewall warning, and will usually need admin rights to run.)

###Multiple Handlers

The second type of constructor takes in a list of MockHttpListener objects, allowing more control over the configuration,
as well as the ability to specify any number of requests to handle.

The following example shows two different HTTP verbs being configured for the same url.  Once you specify an explicit
verb for a URL, only that verb will be accepted, so in this case, if you tried calling that URL with a DELETE verb,
you would get the message: "No handler provided for URL: /data".  You can also use a comma-separated list of methods
if you want to handle multiple, but not all.

``` C#
var client = new RestClient($"http://localhost:{TestPort}/");
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/data", "GET", (req, rsp, prm) => "Get"),
    new MockHttpHandler("/data", "POST", (req, rsp, prm) => "Post").
    new MockHttpHandler("/data-multi", "GET,POST", (req, rsp, prm) => "Get/Post")
};

using (new MockServer(TestPort, requestHandlers))
{
    var result = client.Execute(new RestRequest("data", Method.GET));
    result = client.Execute(new RestRequest("data", Method.POST));
    result = client.Execute(new RestRequest("data", Method.DELETE)); //does not work
    result = client.Execute(new RestRequest("data-multi", Method.GET));
    result = client.Execute(new RestRequest("data-multi", Method.POST));
}
```

When specifying a list of handlers, as above, you can also specify a shared pre-handler that will be run before all
handlers (and even if no handler is found).  It takes in the same request, response and parameter variables as a
normal handler, but does not return a value.  This is great for adding standard headers to all responses, or checking
requests for common information, such as authentication.

``` C#
using (var mockServer = new MockServer(TestPort, requestHandlers, (req, rsp, prm) => rsp.Header("custom", "value")))
{
    ...
}
```

If you need to update your handlers after the server is created, you can easily do that as well.

``` C#
mockServer.ClearRequestHandlers();
mockServer.AddRequestHandler(myHandler);
mockServer.SetRequestHandlers(myHandlers);
```

###Admin Requirements

Due to security restrictions within Windows, the application must be run as an admin for MockServer to function.  For
instances where this is not possible, there are two options.

You can add an exemption for a specific port that all users can listen on using the 'netsh' command (from an Admin
command prompt).

``` Dos
netsh http add urlacl url=http://*:3333/ user=Everyone listen=yes
```

Note that earlier version of Windows used true/false instead of yes/no for the 'listen' parameter, so try that if you get
a 'The parameter is incorrect' failure.

If you are not able to execute the previous command as an admin, but have flexibility on what port you are using, you
can use the following command to find any ports that are already open and use them.

``` Dos
netsh http show urlacl
```

Just look for a reserved URL similar to http://*:3333/ or http://+:3333/.  Note also, that when using a netsh exemption,
the MockServer must match that url EXACTLY.  By default, it uses an '*', so if you can only find one that uses a '+'
(which works identically), then you can pass that character in as the last optional parameter to the MockServer
constructor.

``` C#
using (new MockServer(TestPort, "", (req, rsp, prm) => "Result Body", '+'))
```

###Detailed Input/Output

Notice that the lambda for the handlers takes in both the request (HttpListenerRequest) and response (HttpListenerResponse)
objects, which allows you full access to additional input information, and the ability to set adiditional output information.

You can read/write headers, cookies, etc, or set the status code of the response.  There is also an extension method
to make it easier to grab the body text of the request.

``` C#
var client = new RestClient($"http://localhost:{TestPort}/");
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/echo", (req, rsp, prm) => req.Content()),
    new MockHttpHandler("/fail", (req, rsp, prm) =>
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

You can return more than just strings as well.  The following example manually sets the output to a buffer, using a built-in
extension method.  It is still just returning plain text, but the buffer could have been any array of bytes, such as an
image or file.

Notice that it has no return value, which tells MockServer to not set the output itself, as it is being taken care of.

``` C#
var client = new RestClient($"http://localhost:{TestPort}/");
using (new MockServer(TestPort, "/api", (req, rsp, prm) =>
{
    var buffer = Encoding.UTF8.GetBytes("Result Text");
    rsp.Content(buffer);
}))
{
    var result = client.Execute(new RestRequest("/api", Method.GET));
}
```

###Extension Methods

As seen in the previous examples, there are multiple built-in extension methods for the request and response objects that
make it easier to work with them.  Many of the response methods simply set properties that could already be set, but they
enable a fluid syntax by chaining multiple methods together.

``` C#
rsp.ContentType("text/plain").StatusCode(404).Content("Resource not found");
```

Below is a list of all extension methods.  Note that Content() returns void, so you can't chain after it.  This is because
once you set the content, data starts being sent, and any other changes to the response are ignored.

``` C#
//request
string Content(this HttpListenerRequest request)

//response
void Content(this HttpListenerResponse response, byte[] buffer)
void Content(this HttpListenerResponse response, Stream stream)
void Content(this HttpListenerResponse response, string value)
HttpListenerResponse ContentType(this HttpListenerResponse response, string contentType)
HttpListenerResponse Cookie(this HttpListenerResponse response, string name, string value, string path = null, string domain = null)
HttpListenerResponse Header(this HttpListenerResponse response, string name, string value)
void JsonTextContent(this HttpListenerResponse response, string value) //same as content, but also sets "Content-Type" to "application/json"
HttpListenerResponse StatusCode(this HttpListenerResponse response, int statusCode)
```

####Custom Extension Methods

While the built-in extension methods cover the basics, it is very easy to write your own for any actions your perform
often.  Perhaps you often return a 404 with a custom message (as was done above), you could create an extension of the
response object that sets the content type, status code and content.

``` C#
public static void NotFound(this HttpListenerResponse response, string message)
{
    response.ContentType("text/plain").StatusCode(404).Content(message);
}
```

There are also two extension methods you should always create when working with Json data, for serializing and deserializing
the data.  These would have been built-in with the others, except they require a dependency on a library to handle the
serialization/deserialization.  Since most people will already be using one, picking one could cause an un-needed
depency on those that use a different one.

Below is a class that creates both methods, and has the code for using RestSharp or Json.Net.  They both work the same,
although Json.Net has some extra features that some people might need (and can properly handle serializing a dynamic
object).

``` C#
public static class MockServerExtensions
{
    public static T JsonToObject<T>(this HttpListenerRequest request)
    {
        return SimpleJson.DeserializeObject<T>(request.Content());    //RestSharp
        //return JsonConvert.DeserializeObject<T>(request.Content()); //Json.Net
    }

    public static void JsonContent(this HttpListenerResponse response, object contentObject)
    {
        var jsonText = SimpleJson.SerializeObject(contentObject);  //RestSharp (doesn't work for dynamic)
        //var jsonText JsonConvert.SerializeObject(contentObject); //Json.Net
        response.ContentType("application/json").Content(jsonText);
    }
}
```

###Parameters

There is built-in support for easily accessing parameters from the query string, or the URL itself.  The third
parameter of the handler lambda is a dictionary of all parameters from the URL and query string combined.  Parameters
in the URL are defined by placing a label inside of {}.  That will act as a wild-card, and store the value along with
the label used.

Helow is an example of using query string parameters.

``` C#
var client = new RestClient($"http://localhost:{TestPort}/");
var requestHandlers = new List<MockHttpHandler>()
{
    new MockHttpHandler("/person?active=true", (req, rsp, prm) => "Active"),
    new MockHttpHandler("/person?active=false", (req, rsp, prm) => "Not Active"),
    new MockHttpHandler("/person", (req, rsp, prm) => $"{prm["person_id"]}, {prm["age"]}")
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
var client = new RestClient($"http://localhost:{TestPort}/");
using (new MockServer(TestPort, "xml/{category}/{id}", (req, rsp, prm) =>
{
    rsp.Headers.Add("Content-Type", "application/xml; charset=utf-8");
    return $"<Value>{prm["category"]} - {prm["id"]}</Value>";
}))
{
    var result = client.Execute(new RestRequest("xml/horror/12345/", Method.POST)); //"<Value>horror - 12345</Value>"
}
```
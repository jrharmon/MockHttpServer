using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RestSharp;

namespace MockHttpServer.UnitTests
{
    [TestClass]
    public class MockHttpServerTests
    {
        private const int TestPort = 3333;

        private RestClient CreateRestClient()
        {
            return new RestClient($"http://localhost:{TestPort}/");
        }

        [TestMethod]
        public void TestContentExtensionMethod()
        {
            var client = CreateRestClient();

            string expectedResult = "Result";
            using (new MockHttpServer(TestPort, "/api", (req, rsp, parms) => req.Content()))
            {
                var result = client.Execute(new RestRequest("/api", Method.POST).AddParameter("text/json", "123", ParameterType.RequestBody));
                Assert.AreEqual("123", result.Content);
            }
        }

        /// <summary>
        /// Setting the output manually, and returning null, allows you to return non-string data, such as an image file
        /// </summary>
        [TestMethod]
        public void TestManuallySetOutput()
        {
            var client = CreateRestClient();

            string expectedResult = "Result";
            using (new MockHttpServer(TestPort, "/api", (req, rsp, parms) =>
            {
                var buffer = Encoding.UTF8.GetBytes(expectedResult);
                rsp.ContentLength64 = buffer.Length;
                rsp.OutputStream.Write(buffer, 0, buffer.Length);
                rsp.OutputStream.Close();
                return null;
            }))
            {
                var result = client.Execute(new RestRequest("/api", Method.POST));
                Assert.AreEqual(expectedResult, result.Content);
            }
        }

        [TestMethod]
        public void TestMultipleUrlHandlers()
        {
            var client = CreateRestClient();
            var requestHandlers = new List<MockHttpHandler>()
            {
                new MockHttpHandler("/", (req, rsp, parms) => "Generic"),
                new MockHttpHandler("/json/", (req, rsp, parms) => @"{""Value"": 64}"),
                new MockHttpHandler("/xml/", (req, rsp, parms) => "<Value>64</Value>")
            };

            using (var mockServer = new MockHttpServer(TestPort, requestHandlers))
            {
                var result = client.Execute(new RestRequest("", Method.POST));
                Assert.AreEqual("Generic", result.Content);

                result = client.Execute(new RestRequest("/json/", Method.POST));
                Assert.AreEqual(@"{""Value"": 64}", result.Content);

                result = client.Execute(new RestRequest("/xml/", Method.POST));
                Assert.AreEqual("<Value>64</Value>", result.Content);

                result = client.Execute(new RestRequest("somepath", Method.POST));
                Assert.AreEqual("No handler provided for URL: /somepath", result.Content);
            }
        }
        
        [TestMethod]
        public void TestRootUrlHandler()
        {
            var client = CreateRestClient();

            string expectedResult = "Result";
            using (new MockHttpServer(TestPort, "", (req, rsp, parms) => expectedResult))
            {
                var result = client.Execute(new RestRequest("", Method.POST));
                Assert.AreEqual(expectedResult, result.Content);

                result = client.Execute(new RestRequest("somepath", Method.POST));
                Assert.AreEqual("No handler provided for URL: /somepath", result.Content);
            }
        }

        /// <summary>
        /// The URLs in the handlers, and those actually used in the HTTP call, can have a forward slash at the beginning or end,
        /// or not, and still match.
        /// </summary>
        [TestMethod]
        public void TestSlashCombinations()
        {
            var client = CreateRestClient();

            using (new MockHttpServer(TestPort, "/person/{id}/", (req, rsp, parms) => parms["id"]))
            {
                var result = client.Execute(new RestRequest("/person/123/", Method.POST));
                Assert.AreEqual("123", result.Content);

                result = client.Execute(new RestRequest("/person/234", Method.POST));
                Assert.AreEqual("234", result.Content);

                result = client.Execute(new RestRequest("person/345", Method.POST));
                Assert.AreEqual("345", result.Content);

                result = client.Execute(new RestRequest("person/456/", Method.POST));
                Assert.AreEqual("456", result.Content);
            }
        }

        [TestMethod]
        public void TestUrlParameters()
        {
            var client = CreateRestClient();

            using (new MockHttpServer(TestPort, "xml/{category}/{id}", (req, rsp, parms) =>
            {
                rsp.Headers.Add("Content-Type", "application/xml; charset=utf-8");
                return $"<Value>{parms["category"]} - {parms["id"]}</Value>";
            }))
            {
                var result = client.Execute(new RestRequest("xml/horror/12345/", Method.POST));

                Assert.AreEqual("<Value>horror - 12345</Value>", result.Content);
                Assert.IsTrue(result.Headers.Any(h => h.Name == "Content-Type"));
                Assert.AreEqual("application/xml; charset=utf-8", result.Headers.Single(h => h.Name == "Content-Type").Value);
            }
        }

        //tests to add
        //query string usage
        //return a null, and manually set the output
        //throw an excpetion in the handler
    }
}

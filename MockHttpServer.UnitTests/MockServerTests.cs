using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RestSharp;

namespace MockHttpServer.UnitTests
{
    [TestClass]
    public class MockServerTests
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

            using (new MockServer(TestPort, "/api", (req, rsp, parms) => req.Content()))
            {
                var result = client.Execute(new RestRequest("/api", Method.POST).AddParameter("text/json", "123", ParameterType.RequestBody));
                Assert.AreEqual("123", result.Content);
            }
        }

        [TestMethod]
        public void TestExceptionInHandler()
        {
            var client = CreateRestClient();
            var errorMessage = "Something was null!!!";

            using (new MockServer(TestPort, "/api", (req, rsp, parms) =>
            {
                throw new NullReferenceException(errorMessage);
            }))
            {
                var result = client.Execute(new RestRequest("/api", Method.POST).AddParameter("text/json", "123", ParameterType.RequestBody));
                Assert.IsTrue(result.Content.Contains(errorMessage));
                Assert.AreEqual(HttpStatusCode.InternalServerError, result.StatusCode);
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

            using (new MockServer(TestPort, "/api", (req, rsp, parms) =>
            {
                var buffer = Encoding.UTF8.GetBytes(expectedResult);
                rsp.ContentLength64 = buffer.Length;
                rsp.OutputStream.Write(buffer, 0, buffer.Length);
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

            using (var mockServer = new MockServer(TestPort, requestHandlers))
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
        public void TestQueryString()
        {
            var client = CreateRestClient();
            var requestHandlers = new List<MockHttpHandler>()
            {
                new MockHttpHandler("/person?active=true", (req, rsp, parms) => "Active"),
                new MockHttpHandler("/person?active=false", (req, rsp, parms) => "Not Active"),
                new MockHttpHandler("/person", (req, rsp, parms) => $"{parms["person_id"]}, {parms["age"]}")
            };
            
            using (new MockServer(TestPort, requestHandlers))
            {
                var result = client.Execute(new RestRequest("/person?person_id=123&age=82", Method.POST));
                Assert.AreEqual("123, 82", result.Content);

                result = client.Execute(new RestRequest("/person?active=true", Method.POST));
                Assert.AreEqual("Active", result.Content);

                result = client.Execute(new RestRequest("/person?active=false", Method.POST));
                Assert.AreEqual("Not Active", result.Content);
            }
        }

        [TestMethod]
        public void TestResponseCode()
        {
            var client = CreateRestClient();
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
                var result = client.Execute(new RestRequest("/succeed", Method.POST));
                Assert.AreEqual("succeed", result.Content);
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                result = client.Execute(new RestRequest("/fail", Method.POST));
                Assert.AreEqual("fail", result.Content);
                Assert.AreEqual(HttpStatusCode.InternalServerError, result.StatusCode);
            }
        }

        [TestMethod]
        public void TestRootUrlHandler()
        {
            var client = CreateRestClient();
            string expectedResult = "Result";

            using (new MockServer(TestPort, "", (req, rsp, parms) => expectedResult))
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

            using (new MockServer(TestPort, "/person/{id}/", (req, rsp, parms) => parms["id"]))
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
        public void TestSpecifiedMethods()
        {
            var client = CreateRestClient();
            var requestHandlers = new List<MockHttpHandler>()
            {
                new MockHttpHandler("/data", "GET", (req, rsp, parms) => "Get"),
                new MockHttpHandler("/data", "POST", (req, rsp, parms) => "Post")
            };

            using (new MockServer(TestPort, requestHandlers))
            {
                var result = client.Execute(new RestRequest("data", Method.GET));
                Assert.AreEqual("Get", result.Content);

                result = client.Execute(new RestRequest("data", Method.POST));
                Assert.AreEqual(@"Post", result.Content);

                result = client.Execute(new RestRequest("data", Method.DELETE));
                Assert.AreEqual("No handler provided for URL: /data", result.Content);
            }
        }

        [TestMethod]
        public void TestSpecifiedWildcard()
        {
            var client = CreateRestClient();
            string expectedResult = "Result";

            using (new MockServer(TestPort, "", (req, rsp, parms) => expectedResult, '+'))
            {
                var result = client.Execute(new RestRequest("", Method.POST));
                Assert.AreEqual(expectedResult, result.Content);
            }
        }

        [TestMethod]
        public void TestUrlParameters()
        {
            var client = CreateRestClient();

            using (new MockServer(TestPort, "xml/{category}/{id}", (req, rsp, parms) =>
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
    }
}

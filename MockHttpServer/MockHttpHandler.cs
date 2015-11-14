using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public class MockHttpHandler
    {
        private Regex _comparisonRegex;
        private List<string> _urlParameterNames = new List<string>(); //stores the names of any parameters in the url (ex. 'books/{category}/{id}') 

        public MockHttpHandler(string url, Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, string> handlerFunction)
        {
            Url = url;
            HandlerFunction = handlerFunction;

            //create a regex for matching Url against a raw url that may have parameter definitions in it
            var comparisonRegexString = url + (url.EndsWith("/") ? "?$" : "/?$"); //make sure all urls end with a forward slash for more consistency when comparing to incoming urls
            comparisonRegexString = (comparisonRegexString.StartsWith("/") ? "^" : "^/") + comparisonRegexString;
            var regex = new Regex(@"{(.*?)}");
            foreach (Match match in regex.Matches(comparisonRegexString))
            {
                comparisonRegexString = comparisonRegexString.Replace(match.Value, @"(.*?)");
                _urlParameterNames.Add(match.Groups[1].Value);
            }
            _comparisonRegex = new Regex(comparisonRegexString);
        }

        public string Url { get; }
        public Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, string> HandlerFunction { get; }

        /// <summary>
        /// Returns true if the Url in this handler matches the raw Url passed in
        /// </summary>
        /// <param name="rawUrl">The part of the url after the 'http://host:port/' part of the complete url</param>
        /// <param name="parameters">If there are parameter definitions in Url, they will be stored here, with
        /// their value from rawUrl.  This will be null when the method returns false.  If the method returns true,
        /// but there are no parameters, then it will be an empty dictionary</param>
        /// <returns></returns>
        public bool MatchesUrl(string rawUrl, out Dictionary<string, string> parameters)
        {
            var match = _comparisonRegex.Match(rawUrl);
            parameters = null;
            if (match.Success)
            {
                parameters = new Dictionary<string, string>();
                for (int i = 0; i < _urlParameterNames.Count; i++)
                    parameters[_urlParameterNames[i]] = match.Groups[i + 1].Value;
            }
            return match.Success;
        }
    }
}

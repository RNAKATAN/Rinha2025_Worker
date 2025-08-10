using System.Text;

namespace Rinha2025_Api.Infra
{
    public class HttpRequestMessageBuilder
    {
        private HttpRequestMessage _httpRequestMessage;

        public HttpRequestMessageBuilder()
        {
            _httpRequestMessage = new HttpRequestMessage();
        }

        public HttpRequestMessage Build()
        {
            return _httpRequestMessage;
        }

        public HttpRequestMessageBuilder AddQueryParameters(string[] queryParameters)
        {
           if (queryParameters.Count() > 0)

                //string url  = string.Concat(_httpRequestMessage.RequestUri.ToString(), "?");
                foreach (string parameter in queryParameters)
                {

                }
            return this;
        }

        public HttpRequestMessageBuilder AddUrl(string url)
        {
            _httpRequestMessage.RequestUri = new Uri(url);
            return this;
        }

        public HttpRequestMessageBuilder AddMethod(HttpMethod httpMethod)
        {
            _httpRequestMessage.Method = httpMethod;
            return this;
        }

        public HttpRequestMessageBuilder AddHeaders(Dictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                _httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            return this;
        }

        public HttpRequestMessageBuilder AddBody(string body)
        {
            _httpRequestMessage.Content = new  StringContent(body, Encoding.UTF8, "application/json");
            return this;
        }
    }
}

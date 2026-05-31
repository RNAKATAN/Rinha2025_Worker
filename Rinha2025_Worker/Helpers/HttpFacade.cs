using Rinha2025_Worker.Contratos;
using System.Text.Json;

namespace Rinha2025_Worker.Infra
{
    public class HttpFacade<T> : IHttpFacade<T> where T : class
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public HttpFacade(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<T> ExecutaTarefa(HttpRequestMessage httpRequestMessage)
        {
            var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var content = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _options)!;
            }

            var errorContent = await httpResponseMessage.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Request error: {(int)httpResponseMessage.StatusCode} - {httpResponseMessage.ReasonPhrase}. Details: {errorContent}");
        }
    }
}

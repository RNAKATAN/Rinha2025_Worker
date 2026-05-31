using System.Text.Json;

namespace Rinha2025_Worker.Helpers
{
    public static class JsonSerializerHelper<T>
    {
        private static readonly JsonSerializerOptions _serializeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions _deserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize(T input) =>
            JsonSerializer.Serialize(input, _serializeOptions);

        public static T Deserialize(string input) =>
            JsonSerializer.Deserialize<T>(input, _deserializeOptions)!;
    }
}

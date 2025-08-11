using Rinha2025_Worker.Domain;
using System.Net.Http;
using System.Text.Json;

namespace Rinha2025_Worker.Helpers
{
    public class JsonSerializerHelper<T>
    {
        public static string Serialize(T ObjetoEntrada)
        {

            var options = new JsonSerializerOptions
            {              
                WriteIndented = true
            };


            return JsonSerializer.Serialize(ObjetoEntrada, options);
        }

        public static T Deserialize(string TextoEntrada)
        {
            T objeto;

            var options = new JsonSerializerOptions
            {
              
                WriteIndented = true
            };


            return JsonSerializer.Deserialize<T>(TextoEntrada, options);
        }

    }
}

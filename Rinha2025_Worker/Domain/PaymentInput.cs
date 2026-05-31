using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Rinha2025_Worker.Domain
{

    public class PaymentInput
    {
        public string? CorrelationId { get; set; }
        public decimal Amount { get; set; }
        public int RetryCount { get; set; }
    }


}

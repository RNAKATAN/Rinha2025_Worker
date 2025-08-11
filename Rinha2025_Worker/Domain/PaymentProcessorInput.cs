using System.Globalization;

namespace Rinha2025_Worker.Domain
{
    public class PaymentProcessorInput
    {
  
        public string? CorrelationId { get; set; }
        public decimal Amount { get; set; }

        public string? RequestedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    }    
}

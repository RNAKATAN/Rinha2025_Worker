namespace Rinha2025_Worker.Domain
{
    public class PaymentsSummary
    {
        public PaymentProcessor? Default { get; set; }
        public PaymentProcessor? Fallback { get; set; }
    }
}

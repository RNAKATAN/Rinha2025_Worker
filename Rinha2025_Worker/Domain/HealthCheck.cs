namespace Rinha2025_Worker.Domain
{
    public class HealthCheck
    {
        public bool Failing { get; set; }
        public int MinResponseTime { get; set; }
    }
}

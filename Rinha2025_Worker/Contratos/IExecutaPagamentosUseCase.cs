using Rinha2025_Worker.Domain;

namespace Rinha2025_Worker.Contratos
{
    public interface IExecutaPagamentosUseCase
    {
        Task<PaymentResponse> Processa(HttpRequestMessage httpRequestMessage);
    }
}

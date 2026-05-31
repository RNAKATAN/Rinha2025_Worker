using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;

namespace Rinha2025_Worker.UseCases
{
    public class ExecutaPagamentosUseCase : IExecutaPagamentosUseCase
    {
        private readonly IHttpFacade<PaymentResponse> _httpFacade;

        public ExecutaPagamentosUseCase(IHttpFacade<PaymentResponse> httpFacade)
        {
            _httpFacade = httpFacade;
        }

        public async Task<PaymentResponse> Processa(HttpRequestMessage httpRequestMessage) =>
            await _httpFacade.ExecutaTarefa(httpRequestMessage);
    }
}

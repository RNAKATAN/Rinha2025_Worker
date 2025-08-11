using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;

namespace Rinha2025_Worker.UseCases
{
    public class ExecutaPagamentosUseCase : IExecutaPagamentosUseCase
    {

        public required string TipoPaymentProcessor { get; set; }

        private IHttpFacade<PaymentResponse> _httpFacade;

        public ExecutaPagamentosUseCase(IHttpFacade<PaymentResponse> httpFacade)
        {
            _httpFacade = httpFacade;
        }

        public async Task<PaymentResponse> Processa(HttpRequestMessage httpRequestMessage)
        {

            var response = await _httpFacade.ExecutaTarefa(httpRequestMessage);

            return response;
        }

    }
}

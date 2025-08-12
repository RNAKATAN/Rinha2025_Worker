using Rinha2025_Api.Infra;
using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;
using Rinha2025_Worker.Helpers;
using Rinha2025_Worker.Infra;
using StackExchange.Redis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Rinha2025_Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        IHttpFacade<HealthCheck> _httpFacade;
        private readonly IExecutaPagamentosUseCase _executaPagamentosUseCase;

        public Worker(ILogger<Worker> logger, IHttpFacade<HealthCheck> httpFacade, IExecutaPagamentosUseCase executaPagamentosUseCase, IConnectionMultiplexer redis)
        {
            _logger = logger;          
            _redis = redis;
            _db = _redis.GetDatabase();
            _httpFacade = httpFacade;
            _executaPagamentosUseCase = executaPagamentosUseCase;

        }

        private async Task SetaCacheHealthCheckAsync(string TipoProcessor, int Saudavel)
        {

            await _db.StringSetAsync(TipoProcessor, Saudavel);

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


           
            Task TaskHealthCheckPayment = Task.Run(async () => await ValidaHealhCheckAsync(stoppingToken));

            

           Task TaskProcessaPagamentos = Task.Run(async () => await DequeueMessagesAsync(stoppingToken));


        }

        private async Task ValidaHealhCheckAsync(CancellationToken stoppingToken)
        {

            Dictionary<string, string> HealthCheckDict = new Dictionary<string, string>
            {
                ["DEFAULT"] = $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments",
                ["FALLBACK"] = $"{Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL_BASE")!}/payments"
            };

            //string urlProcessor = TipoProcessor == "DEFAULT" ?   $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments" :  $"{Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL_BASE")!}/payments";

            await SetaCacheHealthCheckAsync("DEFAULT", 0);
            await SetaCacheHealthCheckAsync("FALLBACK", 0);

            do
            {
                HealthCheck healthCheck = new HealthCheck();

                foreach (var dict in HealthCheckDict)
                {
                    try
                    {
                        healthCheck = await ChamaHealthCheckProcessor(dict.Value);
                    }
                    catch (Exception ex)
                    {
                        await SetaCacheHealthCheckAsync(dict.Key, 0);

                    }

                    if (!healthCheck.Failing && healthCheck.MinResponseTime < 30)
                    {
                        await SetaCacheHealthCheckAsync(dict.Key, 1);

                    }
                    else
                    {
                        await SetaCacheHealthCheckAsync(dict.Key, 0);
                    }
                }
                ;
                await Task.Delay(5000);
            }while(!stoppingToken.IsCancellationRequested);
        }

        private async Task<HealthCheck> ChamaHealthCheckProcessor(string urlProcessor)
        {
            HttpRequestMessage requestMessageHealthCheck = new HttpRequestMessageBuilder()
            .AddUrl($"{urlProcessor}/service-health")
            .AddMethod(HttpMethod.Get)
            .Build();

            return await _httpFacade.ExecutaTarefa(requestMessageHealthCheck);
        }

        public async Task DequeueMessagesAsync(CancellationToken stoppingToken)
        {
            
            int tamanho = 10;

            SemaphoreSlim _semaforo = new SemaphoreSlim(6);

           do
            {


                try
                {
                    // Blocking pop from the left (head) of the list with a timeout
                    var result = await _db.ListLeftPopAsync("pagamentos", tamanho);





                    if (result is not null && result.Length > 0)
                    {
                        Console.WriteLine("CHAMANDO O REDIS");
                        var tasks = result.Select(async (pagamento) =>
                        {
                            await _semaforo.WaitAsync(stoppingToken);
                            try
                            {
                                PaymentInput paymentInput = JsonSerializerHelper<PaymentInput>.Deserialize(pagamento);
                                await Processa(paymentInput);
                            }
                            finally
                            {
                                _semaforo.Release();
                            }

                        });

                        await Task.WhenAll(tasks);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"erro: {ex.Message}");
                }
            }while (!stoppingToken.IsCancellationRequested);
        }

        public async Task Processa(PaymentInput paymentInput)
        {

            try
            {


                string urlProcessorDefault = $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments";
                string urlProcessorFallback = $"{Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL_BASE")!}/payments";

                if (await _db.StringGetAsync("DEFAULT") == 1)
                {
                    HttpRequestMessage request = new HttpRequestMessageBuilder()
                        .AddUrl(urlProcessorDefault)

                        .AddBody(JsonSerializerHelper<PaymentProcessorInput>.Serialize(ConverteEmPaymentProcessorInput(paymentInput)))
                        .AddMethod(HttpMethod.Post)
                        .Build();
                    var respostaProcessamento = await _executaPagamentosUseCase.Processa(request);

                }
                else if (await _db.StringGetAsync("FALLBACK") == 1)
                {
                    HttpRequestMessage request = new HttpRequestMessageBuilder()
                        .AddUrl(urlProcessorFallback)
                        .AddBody(JsonSerializerHelper<PaymentProcessorInput>.Serialize(ConverteEmPaymentProcessorInput(paymentInput)))
                        .AddMethod(HttpMethod.Post)
                        .Build();
                    var respostaProcessamento = await _executaPagamentosUseCase.Processa(request);
                }
                else
                {
                    await _db.ListLeftPushAsync("pagamentos", JsonSerializerHelper<PaymentInput>.Serialize(paymentInput));
                    Console.WriteLine("Incluido na fila novamente. Os dois payment processor estao indisponiveis");
                }


            }
            catch (Exception ex)
            {
                await _db.ListLeftPushAsync("pagamentos", JsonSerializerHelper<PaymentInput>.Serialize(paymentInput));
                Console.WriteLine("Incluido na fila novamente. Erro na chamada do payment processor");
            }

        }

        private PaymentProcessorInput ConverteEmPaymentProcessorInput(PaymentInput paymentInput)
        {
            return new PaymentProcessorInput
            {
                Amount = paymentInput.Amount,
                CorrelationId = paymentInput.CorrelationId
            };
        }

    }
}

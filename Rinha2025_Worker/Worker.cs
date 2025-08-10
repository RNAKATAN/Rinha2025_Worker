using Rinha2025_Api.Infra;
using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;
using Rinha2025_Worker.Infra;
using StackExchange.Redis;

namespace Rinha2025_Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        IHttpFacade<HealthCheck> _httpFacade;

        public Worker(ILogger<Worker> logger, IHttpFacade<HealthCheck> httpFacade)
        {
            _logger = logger;
            _redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = _redis.GetDatabase();
            _httpFacade = httpFacade;
        }

        private async Task SetaCacheHealthCheckAsync(string TipoProcessor, int Saudavel)
        {

            await _db.StringSetAsync(TipoProcessor, Saudavel);

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            Task TaskHealthCheckPaymentDefault = Task.Run(async () => await ValidaHealhCheckAsync("DEFAULT", stoppingToken));

            Task TaskHealthCheckPaymentFallback = Task.Run(async () => await ValidaHealhCheckAsync("FALLBACK", stoppingToken));


        }

        private async Task ValidaHealhCheckAsync(string TipoProcessor, CancellationToken stoppingToken)
        {

            string urlProcessor = TipoProcessor == "DEFAULT" ?   $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments" :  $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments";

            await SetaCacheHealthCheckAsync(TipoProcessor, 0);

            HealthCheck healthCheck = new HealthCheck();

            do
            {
                try
                {
                    healthCheck = await ChamaHealthCheckProcessor(urlProcessor);
                }
                catch (Exception ex)
                {
                    await SetaCacheHealthCheckAsync(TipoProcessor, 0);                    

                }

                if (!healthCheck.Failing && healthCheck.MinResponseTime < 30)
                {
                    await SetaCacheHealthCheckAsync(TipoProcessor, 1);

                }
                else
                {
                    await SetaCacheHealthCheckAsync(TipoProcessor, 0);
                }

                Task.Delay(5000);



            } while (!stoppingToken.IsCancellationRequested);
        }

        private async Task<HealthCheck> ChamaHealthCheckProcessor(string urlProcessor)
        {
            HttpRequestMessage requestMessageHealthCheck = new HttpRequestMessageBuilder()
            .AddUrl($"{urlProcessor}/service-health")
            .AddMethod(HttpMethod.Get)
            .Build();

            return await _httpFacade.ExecutaTarefa(requestMessageHealthCheck);
        }


    }
}

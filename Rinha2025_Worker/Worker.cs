using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;
using Rinha2025_Worker.Helpers;
using StackExchange.Redis;

namespace Rinha2025_Worker
{
    public class Worker : BackgroundService
    {
        private const int DequeueBatchSize = 8;
        private const int MaxConcurrentPayments = 4;
        private const int HealthCheckIntervalMs = 5000;
        private const int EmptyQueueDelayMs = 50;

        private readonly ILogger<Worker> _logger;
        private readonly IDatabase _db;
        private readonly IHttpFacade<HealthCheck> _httpFacade;
        private readonly IExecutaPagamentosUseCase _executaPagamentosUseCase;
        private readonly string _urlProcessorDefault;
        private readonly string _urlProcessorFallback;

        public Worker(
            ILogger<Worker> logger,
            IHttpFacade<HealthCheck> httpFacade,
            IExecutaPagamentosUseCase executaPagamentosUseCase,
            IConnectionMultiplexer redis)
        {
            _logger = logger;
            _db = redis.GetDatabase();
            _httpFacade = httpFacade;
            _executaPagamentosUseCase = executaPagamentosUseCase;
            _urlProcessorDefault = $"{Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL_BASE")!}/payments";
            _urlProcessorFallback = $"{Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL_BASE")!}/payments";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(
                ValidaHealthCheckAsync(stoppingToken),
                DequeueMessagesAsync(stoppingToken)
            );
        }

        private async Task ValidaHealthCheckAsync(CancellationToken stoppingToken)
        {
            var endpoints = new Dictionary<string, string>
            {
                ["DEFAULT"] = _urlProcessorDefault,
                ["FALLBACK"] = _urlProcessorFallback
            };

            await _db.StringSetAsync("DEFAULT", 0);
            await _db.StringSetAsync("FALLBACK", 0);

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var (key, url) in endpoints)
                {
                    try
                    {
                        var healthCheck = await CallHealthCheckAsync(url);
                        var isHealthy = !healthCheck.Failing && healthCheck.MinResponseTime < 30;
                        await _db.StringSetAsync(key, isHealthy ? 1 : 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Health check failed for {Processor}", key);
                        await _db.StringSetAsync(key, 0);
                    }
                }

                await Task.Delay(HealthCheckIntervalMs, stoppingToken);
            }
        }

        private async Task<HealthCheck> CallHealthCheckAsync(string paymentsUrl)
        {
            var request = new HttpRequestMessageBuilder()
                .AddUrl($"{paymentsUrl}/service-health")
                .AddMethod(HttpMethod.Get)
                .Build();

            return await _httpFacade.ExecutaTarefa(request);
        }

        private async Task DequeueMessagesAsync(CancellationToken stoppingToken)
        {
            var semaphore = new SemaphoreSlim(MaxConcurrentPayments);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var results = await _db.ListLeftPopAsync("pagamentos", DequeueBatchSize);

                    if (results is not null && results.Length > 0)
                    {
                        var tasks = results.Select(async item =>
                        {
                            await semaphore.WaitAsync(stoppingToken);
                            try
                            {
                                var paymentInput = JsonSerializerHelper<PaymentInput>.Deserialize(item!);
                                await ProcessPaymentAsync(paymentInput);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    }
                    else
                    {
                        await Task.Delay(EmptyQueueDelayMs, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dequeuing payments");
                }
            }
        }

        private async Task ProcessPaymentAsync(PaymentInput paymentInput)
        {
            try
            {
                string? targetUrl = null;

                if (await _db.StringGetAsync("DEFAULT") == 1)
                    targetUrl = _urlProcessorDefault;
                else if (await _db.StringGetAsync("FALLBACK") == 1)
                    targetUrl = _urlProcessorFallback;

                if (targetUrl is null)
                {
                    await RequeueAsync(paymentInput);
                    _logger.LogWarning("Both payment processors unavailable — requeued payment {CorrelationId}", paymentInput.CorrelationId);
                    return;
                }

                var request = new HttpRequestMessageBuilder()
                    .AddUrl(targetUrl)
                    .AddBody(JsonSerializerHelper<PaymentProcessorInput>.Serialize(ToProcessorInput(paymentInput)))
                    .AddMethod(HttpMethod.Post)
                    .Build();

                await _executaPagamentosUseCase.Processa(request);
            }
            catch (Exception ex)
            {
                await RequeueAsync(paymentInput);
                _logger.LogError(ex, "Payment processor call failed — requeued payment {CorrelationId}", paymentInput.CorrelationId);
            }
        }

        private async Task RequeueAsync(PaymentInput paymentInput) =>
            await _db.ListLeftPushAsync("pagamentos", JsonSerializerHelper<PaymentInput>.Serialize(paymentInput));

        private static PaymentProcessorInput ToProcessorInput(PaymentInput input) =>
            new() { CorrelationId = input.CorrelationId, Amount = input.Amount };
    }
}

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
        private const int RetryQueuePollMs = 500;
        private const int MaxRetries = 5;
        private const string PaymentQueueKey = "pagamentos";
        private const string RetryQueueKey = "pagamentos:retry";

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
                DequeueMessagesAsync(stoppingToken),
                ProcessRetryQueueAsync(stoppingToken)
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
                    var results = await _db.ListLeftPopAsync(PaymentQueueKey, DequeueBatchSize);

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

        private async Task ProcessRetryQueueAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var due = await _db.SortedSetRangeByScoreAsync(RetryQueueKey, 0, nowMs);

                    if (due.Length > 0)
                    {
                        foreach (var entry in due)
                            await _db.ListRightPushAsync(PaymentQueueKey, entry);

                        await _db.SortedSetRemoveRangeByScoreAsync(RetryQueueKey, 0, nowMs);

                        _logger.LogInformation("Moved {Count} payment(s) from retry queue back to main queue", due.Length);
                    }
                    else
                    {
                        await Task.Delay(RetryQueuePollMs, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing retry queue");
                    await Task.Delay(RetryQueuePollMs, stoppingToken);
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
                    await ScheduleRetryAsync(paymentInput, "both payment processors unavailable");
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
                await ScheduleRetryAsync(paymentInput, ex.Message);
            }
        }

        private async Task ScheduleRetryAsync(PaymentInput paymentInput, string reason)
        {
            paymentInput.RetryCount++;

            if (paymentInput.RetryCount > MaxRetries)
            {
                _logger.LogError(
                    "Payment {CorrelationId} exceeded {MaxRetries} retries — discarding. Last reason: {Reason}",
                    paymentInput.CorrelationId, MaxRetries, reason);
                return;
            }

            // 1s, 2s, 4s, 8s, 16s
            var delayMs = (long)(Math.Pow(2, paymentInput.RetryCount - 1) * 1000);
            var retryAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMs;

            await _db.SortedSetAddAsync(
                RetryQueueKey,
                JsonSerializerHelper<PaymentInput>.Serialize(paymentInput),
                retryAtMs);

            _logger.LogWarning(
                "Payment {CorrelationId} scheduled for retry {Attempt}/{MaxRetries} in {DelayMs}ms. Reason: {Reason}",
                paymentInput.CorrelationId, paymentInput.RetryCount, MaxRetries, delayMs, reason);
        }

        private static PaymentProcessorInput ToProcessorInput(PaymentInput input) =>
            new() { CorrelationId = input.CorrelationId, Amount = input.Amount };
    }
}

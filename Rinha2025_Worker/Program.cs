using Rinha2025_Worker.Contratos;
using Rinha2025_Worker.Domain;
using Rinha2025_Worker.Infra;
using Rinha2025_Worker.UseCases;
using StackExchange.Redis;

namespace Rinha2025_Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            var redisConfig = new ConfigurationOptions
            {
                EndPoints =
                {
                    {
                        Environment.GetEnvironmentVariable("REDIS_HOST")!,
                        int.Parse(Environment.GetEnvironmentVariable("REDIS_PORT")!)
                    }
                },
                AllowAdmin = true,
                AbortOnConnectFail = false
            };

            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig));
            builder.Services.AddHttpClient<IHttpFacade<HealthCheck>, HttpFacade<HealthCheck>>();
            builder.Services.AddHttpClient<IHttpFacade<PaymentResponse>, HttpFacade<PaymentResponse>>();
            builder.Services.AddTransient<IExecutaPagamentosUseCase, ExecutaPagamentosUseCase>();
            builder.Services.AddHostedService<Worker>();

            builder.Build().Run();
        }
    }
}

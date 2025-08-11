using Microsoft.Extensions.DependencyInjection;
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
            builder.Services.AddHostedService<Worker>();

            var configurationOption = new ConfigurationOptions
            {
                EndPoints = { { Environment.GetEnvironmentVariable("REDIS_HOST")!.ToString(), int.Parse(Environment.GetEnvironmentVariable("REDIS_PORT")!) } },
                AllowAdmin = true,
                AbortOnConnectFail = false
                
                
                
            };

            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(configurationOption));

            builder.Services.AddHttpClient<IHttpFacade<HealthCheck>, HttpFacade<HealthCheck>>();
            builder.Services.AddTransient<IExecutaPagamentosUseCase, ExecutaPagamentosUseCase>();
           // builder.Services.AddHttpClient<IHttpFacade<PaymentInput>, HttpFacade<PaymentInput>>();
            builder.Services.AddHttpClient<IHttpFacade<PaymentResponse>, HttpFacade<PaymentResponse>>();


            var host = builder.Build();
            host.Run();
        }
    }
}
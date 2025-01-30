using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.RabbitMq;

namespace Rebus.RabbitMq.Tests;

[SetUpFixture]
public class RabbitMqTestContainerManager
{
    static RabbitMqContainer _container;

    [OneTimeSetUp]
    public async Task StartRabbitMq()
    {
        Console.WriteLine("Starting RabbitMQ test container");

        _container = new RabbitMqBuilder().Build();
        await _container.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopRabbitMq()
    {
        Console.WriteLine("Stopping RabbitMQ test container");

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public static string GetConnectionString() => _container.GetConnectionString();

    public static CustomContainer GetCustomContainer(Func<RabbitMqBuilder, RabbitMqBuilder> build)
    {
        var rabbitMqBuilder = build(new RabbitMqBuilder());
        var container = rabbitMqBuilder.Build();
        container.StartAsync().GetAwaiter().GetResult();
        return new CustomContainer(container.GetConnectionString(), () =>
        {
            Task.Run(async () =>
                {
                    await container.StopAsync();
                    await container.DisposeAsync();
                })
                .GetAwaiter().GetResult();
        });
    }

    public class CustomContainer : IDisposable
    {
        readonly Action _dispose;

        public string ConnnectionString { get; }

        public CustomContainer(string connnectionString, Action dispose)
        {
            _dispose = dispose;
            ConnnectionString = connnectionString;
        }

        public void Dispose() => _dispose();
    }
}
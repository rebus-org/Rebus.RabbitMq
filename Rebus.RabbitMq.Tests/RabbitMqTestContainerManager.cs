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
}
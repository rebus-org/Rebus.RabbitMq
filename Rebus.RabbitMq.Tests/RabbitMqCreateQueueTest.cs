using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable UnusedVariable

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqCreateQueueTest : FixtureBase
{
    [Test]
    public async Task Test_CreateQueue_WHEN_InputQueueOptions_AutoDelete_False_AND_TTL0_THEN_BusCanStart_()
    {
        using var testScope = new QueueNameTestScope();
        using var activator = new BuiltinHandlerActivator();

        var configurer = Configure.With(activator)
            .Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, testScope.QueueName)
                    .InputQueueOptions(o => o.SetAutoDelete(false))
                    .AddClientProperties(new Dictionary<string, string> {
                        { "description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs" }
                    });
            });

        using (var bus = configurer.Start())
        {
            Assert.That(bus.Advanced.Workers.Count, Is.GreaterThan(0));
        }

        Thread.Sleep(5000);
        Assert.That(await RabbitMqTransportFactory.QueueExists(testScope.QueueName), Is.True, $"The queue '{testScope.QueueName}' does not exist");
    }

    [Test]
    public void Test_CreateQueue_WHEN_InputQueueOptions_AutoDelete_True_THEN_BusCanStart()
    {
        using var testScope = new QueueNameTestScope();

        using var activator = new BuiltinHandlerActivator();

        var configurer = Configure.With(activator)
            .Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, testScope.QueueName)
                    .InputQueueOptions(o => o.SetAutoDelete(true))
                    .AddClientProperties(new Dictionary<string, string> {
                        { "description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs" }
                    });
            });

        var bus = configurer.Start();

        Assert.That(bus.Advanced.Workers.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Test_CreateQueue_WHEN_InputQueueOptions_SetQueueTTL_0_THEN_ArgumentException()
    {
        using var testScope = new QueueNameTestScope();

        using var activator = new BuiltinHandlerActivator();

        void InitializeWithZeroTtl()
        {
            var configurer = Configure
                .With(activator)
                .Transport(t =>
                {
                    t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, testScope.QueueName)
                        .InputQueueOptions(o => o.SetQueueTTL(0).SetDurable(false))
                        .AddClientProperties(new Dictionary<string, string>
                            {{"description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs"}});
                });
        }

        Assert.Throws<ArgumentException>(InitializeWithZeroTtl, "Time must be in milliseconds and greater than 0");
    }

    [Test]
    public async Task Test_CreateQueue_WHEN_InputQueueOptions_SetQueueTTL_5000_THEN_QueueIsDeleted_WHEN_5000msAfterConnectionClosed()
    {
        using var testScope = new QueueNameTestScope();

        using (var activator = new BuiltinHandlerActivator())
        {
            var configurer = Configure.With(activator)
                .Transport(t =>
                {
                    t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, testScope.QueueName)
                        .InputQueueOptions(o => o.SetQueueTTL(100))
                        .AddClientProperties(new Dictionary<string, string>
                        {
                            {"description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs"}
                        });
                });

            var bus = configurer.Start();

            Assert.That(bus.Advanced.Workers.Count, Is.GreaterThan(0));
        }


        Thread.Sleep(5000);
        Assert.That(await RabbitMqTransportFactory.QueueExists(testScope.QueueName), Is.False, $"The queue '{testScope.QueueName}' was still there");
    }

    class QueueNameTestScope : IDisposable
    {
        public string QueueName { get; } = Guid.NewGuid().ToString();

        public void Dispose() => RabbitMqTransportFactory.DeleteQueue(QueueName).GetAwaiter().GetResult();
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqPriorityQueueTest : FixtureBase
{
    readonly string _priorityQueueName = TestConfig.GetName("priority-queue");

    protected override void SetUp()
    {
        RabbitMqTransportFactory.DeleteQueue(_priorityQueueName).GetAwaiter().GetResult();
    }

    [Test]
    public void PriorityInputQueueCreate()
    {
        // Start a server with priority
        Using(StartServer(_priorityQueueName, 10));

        // Check if queues exists
        Assert.That(async () =>
        {
            var connectionFactory = new ConnectionFactory { Uri = new Uri(RabbitMqTransportFactory.ConnectionString) };

            await using var connection = await connectionFactory.CreateConnectionAsync();

            await using var model = await connection.CreateChannelAsync();

            // Throws exception if queue paramters differ
            await model.QueueDeclareAsync(_priorityQueueName,
                exclusive: false,
                durable: true,
                autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    {"x-max-priority", 10}
                });
        }, Throws.Nothing);
    }

    [Test]
    public void PriorityInputQueueCreateThrowsOnDifferentPriority()
    {
        // Start a server with priority
        Using(StartServer(_priorityQueueName, 10));

        Assert.That(async () =>
        {
            var connectionFactory = new ConnectionFactory { Uri = new Uri(RabbitMqTransportFactory.ConnectionString) };

            await using var connection = await connectionFactory.CreateConnectionAsync();

            await using var model = await connection.CreateChannelAsync();

            await model.QueueDeclareAsync(_priorityQueueName,
                exclusive: false,
                durable: true,
                autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    {"x-max-priority", 1}
                });
        }, Throws.TypeOf<OperationInterruptedException>());
    }

    [Test]
    [Description("TODO: mhg Fix it so that it disposes everything properly")]
    public void MultipleServersThrowsOnDifferentPriority()
    {
        StartServer(_priorityQueueName, 10);

        // Rebus throws resolution exception
        // NOTE: Would be nice if this could be a specific RebusApplicationException
        var resolutionException = Assert.Throws<ResolutionException>(() =>
        {
            StartServer(_priorityQueueName, 1);
        });

        Console.WriteLine(resolutionException);
    }

    [Test]
    public async Task PriorityQueue()
    {
        // Setup priority constants
        const int maxPriority = 10;
        const int expectedMessageCount = 30;

        var handledSequence = new List<int>();
        var gotMessages = new ManualResetEvent(false);

        var activator = StartServer(_priorityQueueName, maxPriority).Handle<string>(async str =>
        {
            Console.WriteLine($"Message arrived: {str}");

            var prio = int.Parse(str);
            handledSequence.Add(prio);

            if (handledSequence.Count == expectedMessageCount)
            {
                gotMessages.Set();
            }
        });

        Using(activator);

        var bus = StartOneWayClient();

        // Random priority
        var rnd = new Random();
        for (var i = 0; i <= 9; i++)
        {
            var priority = rnd.Next(0, 10);
            var message = priority.ToString();
            Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

            await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
            {
                [RabbitMqHeaders.Priority] = priority.ToString(),
                [RabbitMqHeaders.DeliveryMode] = "1" // transient
            });
        }

        // Low priority first
        for (var i = 0; i <= 9; i++)
        {
            var priority = i;
            var message = priority.ToString();
            Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

            await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
            {
                [RabbitMqHeaders.Priority] = priority.ToString(),
                [RabbitMqHeaders.DeliveryMode] = "1"
            });
        }

        // High priority first
        for (var i = 0; i <= 9; i++)
        {
            var priority = 9 - i;
            var message = priority.ToString();
            Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

            await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
            {
                [RabbitMqHeaders.Priority] = priority.ToString(),
                [RabbitMqHeaders.DeliveryMode] = "1"
            });
        }

        activator.Bus.Advanced.Workers.SetNumberOfWorkers(1);

        Console.WriteLine("Waiting for all message to arrive");

        gotMessages.WaitOrDie(TimeSpan.FromSeconds(5));

        Console.WriteLine("Got all messages :)");

        var error = false;
        int? last = null;
        foreach (var prio in handledSequence)
        {
            Console.WriteLine($"Sequence: {prio}");
            if (last.HasValue && last.Value < prio)
            {
                error = true;
            }

            last = prio;
        }

        Assert.That(handledSequence.Count, Is.EqualTo(expectedMessageCount), $"Expected {expectedMessageCount} messages");
        Assert.That(error, Is.False, "Sequence is out of order");
    }

    BuiltinHandlerActivator StartServer(string queueName, int maxPriority)
    {
        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                .StrictPriorityQueue(maxPriority))
            .Options(o =>
            {
                o.SetNumberOfWorkers(0);
            }).Start();

        return activator;
    }

    private IBus StartOneWayClient()
    {
        var client = Using(new BuiltinHandlerActivator());

        return Configure.With(client)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqTransportFactory.ConnectionString))
            .Start();
    }
}
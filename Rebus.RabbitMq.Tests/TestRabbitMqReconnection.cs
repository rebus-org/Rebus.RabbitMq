using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Testcontainers.RabbitMq;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
[Description("Simulates a lost connection by restarting RabbitMQ while an endpoint is receiving messages")]
public class TestRabbitMqReconnection : FixtureBase
{
    readonly string _receiverQueueName = TestConfig.GetName("receiver");

    RabbitMqContainer _rabbitMqContainer;
    BuiltinHandlerActivator _receiver;
    string _initialConnectionString;
    IBus _sender;

    protected override void SetUp()
    {
        // We have to force the port here, otherwise restarting the container will give us a new port
        // which obviously won't work with the previous connection string.
        _rabbitMqContainer = new RabbitMqBuilder().WithPortBinding(25672, 5672).Build();
        _rabbitMqContainer.StartAsync().GetAwaiter().GetResult();

        Thread.Sleep(5000);

        _initialConnectionString = _rabbitMqContainer.GetConnectionString();
        
        using (var transport = new RabbitMqTransport(_initialConnectionString, _receiverQueueName, new NullLoggerFactory()))
        {
            transport.PurgeInputQueue().GetAwaiter().GetResult();
        }

        _receiver = Using(new BuiltinHandlerActivator());

        Configure.With(_receiver)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseRabbitMq(_initialConnectionString, _receiverQueueName).Prefetch(1))
            .Options(o =>
            {
                o.SetNumberOfWorkers(0);
                o.SetMaxParallelism(1);
            })
            .Start();

        var senderActivator = Using(new BuiltinHandlerActivator());

        _sender = Configure.With(senderActivator)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseRabbitMqAsOneWayClient(_initialConnectionString))
            .Routing(r => r.TypeBased().MapFallback(_receiverQueueName))
            .Start();
    }

    protected override void TearDown()
    {
        _rabbitMqContainer.StopAsync().GetAwaiter().GetResult();
        _rabbitMqContainer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.TearDown();
    }

    [Test]
    public void WeGetAllMessagesEvenThoughRabbitMqRestarts()
    {
        var messages = new ConcurrentDictionary<string, bool>();
        
        _receiver.Handle<string>(async message =>
        {
            Console.WriteLine($"Received '{message}'");
            await Task.Delay(500);
            messages[message] = true;
        });
        
        _receiver.Bus.Advanced.Workers.SetNumberOfWorkers(1);

        Console.WriteLine("Sending messages...");

        Enumerable.Range(0, 40)
            .Select(i => $"message number {i}")
            .ToList()
            .ForEach(message =>
            {
                messages[message] = false;
                _sender.Send(message).Wait();
            });

        Console.WriteLine("Waiting for all messages to have been handled...");

        // restart RabbitMQ while we are receiving messages
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            Thread.Sleep(5000);
            Console.WriteLine("Stopping RabbitMQ....");
            await _rabbitMqContainer.StopAsync();
            Thread.Sleep(1000);
            
            Console.WriteLine("Starting RabbitMQ....");
            await _rabbitMqContainer.StartAsync();
            Assert.That(_rabbitMqContainer.GetConnectionString(), Is.EqualTo(_initialConnectionString));
        });

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            Thread.Sleep(1000);

            if (messages.All(kvp => kvp.Value))
            {
                Console.WriteLine("All messages received :)");
                break;
            }

            var received = messages.Count(v => v.Value);
                
            Console.WriteLine($"Messages correctly received at this point: {received}");
            if ((_rabbitMqContainer.State & TestcontainersStates.Running) != 0)
            {
                Assert.That(_rabbitMqContainer.GetConnectionString(), Is.EqualTo(_initialConnectionString));
            }

            if (stopwatch.Elapsed < TimeSpan.FromMinutes(2)) continue;

            throw new TimeoutException("Waited too long!");
        }
    }
}
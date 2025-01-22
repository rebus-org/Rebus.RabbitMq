using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Testcontainers.RabbitMq;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqRecovery : FixtureBase
{
    static readonly string QueueName = TestConfig.GetName("recoverytest");

    // This seems to be the same as TestRabbitMqReconnection.WeGetAllMessagesEvenThoughRabbitMqRestarts or am I
    // missing something?
    [Test]
    public async Task VerifyThatEndpointCanRecoverAfterLosingRabbitMqConnection()
    {
        var rabbitMqContainer = new RabbitMqBuilder().WithPortBinding(25673, 5672).Build();
        await rabbitMqContainer.StartAsync();
        int numberOfMessages = 100;
        const int millisecondsDelay = 300;

        var expectedTestDuration = TimeSpan.FromMilliseconds(numberOfMessages * millisecondsDelay);

        Console.WriteLine($"Expected test duration {expectedTestDuration}");

        using var activator = new BuiltinHandlerActivator();
        
        var receivedMessages = 0;
        var allMessagesReceived = new ManualResetEvent(false);

        activator.Handle<string>(async _ =>
        {
            await Task.Delay(millisecondsDelay);

            receivedMessages++;

            if (receivedMessages == numberOfMessages)
            {
                allMessagesReceived.Set();
            }
        });

        Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(rabbitMqContainer.GetConnectionString(), QueueName))
            .Options(o =>
            {
                o.SetNumberOfWorkers(0);
                o.SetMaxParallelism(1);
                o.RetryStrategy(maxDeliveryAttempts: 1);
            })
            .Start();

        Console.WriteLine($"Sending {numberOfMessages} messages");

        Enumerable.Range(0, numberOfMessages)
            .Select(i => $"this is message {i}")
            .ToList()
            .ForEach(message => activator.Bus.SendLocal(message).Wait());

        Console.WriteLine("Starting receiver");

        activator.Bus.Advanced.Workers.SetNumberOfWorkers(1);

        Console.WriteLine("Waiting a short while");

        Thread.Sleep(5000);

        Console.WriteLine("Stopping RabbitMQ service");

        await rabbitMqContainer.StopAsync();

        Console.WriteLine("Waiting a short while");

        Thread.Sleep(5000);

        Console.WriteLine("Starting RabbitMQ service");

        await rabbitMqContainer.StartAsync();

        Console.WriteLine("Waiting for the last messages");

        allMessagesReceived.WaitOrDie(TimeSpan.FromMinutes(5));
        allMessagesReceived.Reset();

        receivedMessages = 0;
        numberOfMessages = 1;
        
        await activator.Bus.SendLocal("A test after recovery");
        
        allMessagesReceived.WaitOrDie(TimeSpan.FromSeconds(5));
        
    }
}
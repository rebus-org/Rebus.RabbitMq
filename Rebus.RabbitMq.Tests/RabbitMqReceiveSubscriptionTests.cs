using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqReceiveSubscriptionTests : FixtureBase
{
    readonly string _publisherQueueName = TestConfig.GetName("publisher-RabbitMqReceiveSubscriptionTests");
    readonly string _subscriberQueueName = TestConfig.GetName("subscriber-RabbitMqReceiveSubscriptionTests");

    protected override void TearDown()
    {
        base.TearDown();
        RabbitMqTransportFactory.DeleteQueue(_publisherQueueName).GetAwaiter().GetResult();
        RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName).GetAwaiter().GetResult();
    }

    [Test]
    public async Task ReceiveOnSubscribe_WHEN_SubscriberQueueDeleted_THEN_ItRecreates_SubscriberQueue_AND_ReceivesPublishedData()
    {
        const string message = "Test-Message-123";

        using var receivedEvent = new ManualResetEvent(false);

        using var publisher = StartBus(_publisherQueueName);

        async Task HandlerMethod(string data)
        {
            if (string.Equals(data, message))
            {
                receivedEvent.Set();
            }
        }

        using var subscriber = StartBus(_subscriberQueueName, HandlerMethod);
            
        await subscriber.Subscribe<string>();

        // remove the input queue
        await RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);

        // wait a short while
        await Task.Delay(TimeSpan.FromSeconds(60));

        // check that published message is received without problems
        await publisher.Publish(message);

        receivedEvent.WaitOrDie(TimeSpan.FromSeconds(2),
            "The event has not been receved by the subscriber within the expected time");
    }

    [Test]
    public async Task ReceiveOnSubscribe_WHEN_SubscriberQueueDeleted_THEN_ItThrowsException()
    {
        const string message = "Test-Message-123";

        using var receivedEvent = new ManualResetEvent(false);

        using var publisher = StartBus(_publisherQueueName);

        async Task HandlerMethod(string data)
        {
            if (string.Equals(data, message))
            {
                receivedEvent.Set();
            }
        }

        using var subscriber = StartBus(_subscriberQueueName, HandlerMethod, false, false);
            
        // create the input queue
        await RabbitMqTransportFactory.CreateQueue(_subscriberQueueName);

        await subscriber.Subscribe<string>();

        // remove the input queue
        await RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);

        // wait a short while
        await Task.Delay(5000);

        // check that published message is received without problems
        await publisher.Publish(message);

        var result = receivedEvent.WaitOne(TimeSpan.FromSeconds(2));
        Assert.That(result, Is.False);
    }

    IBus StartBus(string queueName, Func<string, Task> handlerMethod = null, bool autoDeclareQueue = true, bool autoDeclareBindQueue = true)
    {
        var activator = new BuiltinHandlerActivator();

        Using(activator);

        if (handlerMethod != null)
        {
            activator.Handle(handlerMethod);
        }

        Configure.With(activator)
            .Transport(t =>
            {
                var properties = new Dictionary<string, string>
                {
                    { "description", "Created for RabbitMqReceiveTests" }
                };

                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                    .AddClientProperties(properties)
                    .Declarations(true, autoDeclareQueue, autoDeclareBindQueue);
            })
            .Start();

        return activator.Bus;
    }
}
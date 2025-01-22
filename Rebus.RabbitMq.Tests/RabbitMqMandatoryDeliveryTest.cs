using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqMandatoryDeliveryTest : FixtureBase
{
    readonly string _noneExistingQueueName = TestConfig.GetName("non-existing-queue");
    readonly string _mandatoryQueue = TestConfig.GetName("mandatory-queue");

    protected override void SetUp()
    {
        RabbitMqTransportFactory.DeleteQueue(_noneExistingQueueName).GetAwaiter().GetResult();
        RabbitMqTransportFactory.DeleteQueue(_mandatoryQueue).GetAwaiter().GetResult();
    }

    [Test]
    public void MandatoryHeaderWithoutHandlerThrows()
    {
        var bus = StartOneWayClient(null);

        Assert.ThrowsAsync<MandatoryDeliveryException>(async () =>
        {
            await bus.Advanced.Routing.Send(_noneExistingQueueName, "I'm mandatory", new Dictionary<string, string>
            {
                [RabbitMqHeaders.Mandatory] = bool.TrueString,
            });
        });
    }

    [Test]
    public async Task MandatoryHeaderCallback()
    {
        var messageId = Guid.NewGuid();
        var gotCallback = new ManualResetEvent(false);

        void Callback(object sender, BasicReturnEventArgs eventArgs)
        {
            if (eventArgs.Message.GetMessageId().Equals(messageId.ToString()))
            {
                gotCallback.Set();
            }
        }

        var bus = StartOneWayClient(Callback);

        try
        {
            await bus.Advanced.Routing.Send(_noneExistingQueueName, "I'm mandatory", new Dictionary<string, string>
            {
                [RabbitMqHeaders.MessageId] = messageId.ToString(),
                [RabbitMqHeaders.Mandatory] = bool.TrueString,
            });
        } catch(RebusApplicationException){}

        gotCallback.WaitOrDie(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Exposes a potential problem with the mandatory delivery method: 
    /// When the mandatory header is used and an exception is thrown in the handler. 
    /// Then the delivery to the error queue throws a MandatoryDeliveryException because a handler for BasicReturn is not configured on the server
    /// 
    /// sender -> mandatory message -> server
    /// server -> handler throws exception
    /// rebus -> transfer to error queue
    /// rabbit transport -> throws MandatoryDeliveryException since server does not have th required handler
    /// </summary>
    /// <returns></returns>
    [Test, Ignore("Exposes an issue with the mandatory delivery")]
    public async Task CanTransferMandatoryHeaderToErrorQueue()
    {
        var recievedMessageOnErrorQueue = new ManualResetEvent(false);

        var inputQueueServer = StartServer(_mandatoryQueue).Handle<string>(str =>
        {
            Console.WriteLine($"Message arrived on queue: {_mandatoryQueue}: {str}");

            throw new Exception("Exception in handler");
        });

        Using(inputQueueServer);

        var errorQueueServer = StartServer("error").Handle<string>(str =>
        {
            Console.WriteLine($"Message arrived on queue: error: {str}");

            recievedMessageOnErrorQueue.Set();

            return Task.FromResult(0);
        });

        Using(errorQueueServer);

        var bus = StartOneWayClient((_, _) => { /* Required eventhandler for mandatory delivery */ });

        await bus.Advanced.Routing.Send(_mandatoryQueue, "I'm mandatory and throws exception", new Dictionary<string, string>
        {
            [RabbitMqHeaders.Mandatory] = bool.TrueString,
        });

        recievedMessageOnErrorQueue.WaitOrDie(TimeSpan.FromSeconds(5));
    }

    IBus StartOneWayClient(Action<object, BasicReturnEventArgs> basicReturnCallback)
    {
        var client = Using(new BuiltinHandlerActivator());

        return Configure.With(client)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqTransportFactory.ConnectionString)
                .Mandatory(basicReturnCallback))
            .Start();
    }

    BuiltinHandlerActivator StartServer(string queueName)
    {
        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName))
            .Options(o => o.RetryStrategy(maxDeliveryAttempts: 1))
            .Start();

        return activator;
    }
}
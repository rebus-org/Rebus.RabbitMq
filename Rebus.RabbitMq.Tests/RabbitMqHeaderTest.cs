using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqHeaderTest : FixtureBase
{
    readonly string _noneExistingQueueName = TestConfig.GetName("non-existing-queue");

    protected override void SetUp()
    {
        RabbitMqTransportFactory.DeleteQueue(_noneExistingQueueName);
    }

    [Test]
    public async Task Headers()
    {
        var messageId = Guid.NewGuid();
        var gotCallback = new ManualResetEvent(false);

        var headersFromCallback = new Dictionary<string, string>();

        void Callback(object sender, BasicReturnEventArgs eventArgs)
        {
            foreach (var kvp in eventArgs.Headers)
            {
                headersFromCallback[kvp.Key] = kvp.Value;
            }
            gotCallback.Set();
        }

        var timestamp = DateTime.Now;
        var headers = new Dictionary<string, string>
        {
            [RabbitMqHeaders.Mandatory] = bool.TrueString, // set in order to have callback
            [RabbitMqHeaders.MessageId] = messageId.ToString(),
            [RabbitMqHeaders.AppId] = Guid.NewGuid().ToString(),
            [RabbitMqHeaders.CorrelationId] = Guid.NewGuid().ToString(),
            [RabbitMqHeaders.UserId] = "guest",
            [RabbitMqHeaders.ContentType] = "text/plain", // NOTE: Gets overridden by JsonSerializer in Rebus
            [RabbitMqHeaders.ContentEncoding] = "none",
            [RabbitMqHeaders.DeliveryMode] = "1",
            [RabbitMqHeaders.Type] = "string",
            [RabbitMqHeaders.Expiration] = TimeSpan.FromMinutes(2).ToString(),
            [RabbitMqHeaders.Timestamp] = timestamp.ToString("s"),
            ["Custom-header"] = "custom",
        };

        var bus = StartOneWayClient(Callback);
        await bus.Advanced.Routing.Send(_noneExistingQueueName, "I have headers", headers);

        gotCallback.WaitOrDie(TimeSpan.FromSeconds(2));

        var keys = new[]
        {
            RabbitMqHeaders.MessageId,
            RabbitMqHeaders.AppId,
            RabbitMqHeaders.CorrelationId,
            RabbitMqHeaders.UserId,
            //RabbitMqHeaders.ContentType, //< we do custom check for this one
            RabbitMqHeaders.ContentEncoding,
            RabbitMqHeaders.DeliveryMode,
            RabbitMqHeaders.Type,
            "Custom-header"
        };

        foreach (var key in keys)
        {
            Assert.That(headersFromCallback[key], Is.EqualTo(headers[key]), $"Values for key '{key}' did not match");
        }

        var contentType = headersFromCallback.GetValue(RabbitMqHeaders.ContentType);

        Assert.That(contentType, Is.Not.Empty);


        //Assert.AreEqual(headers[RabbitMqHeaders.MessageId], basicProperties.MessageId, RabbitMqHeaders.MessageId);
        //Assert.AreEqual(headers[RabbitMqHeaders.AppId], basicProperties.AppId, RabbitMqHeaders.AppId);
        //Assert.AreEqual(headers[RabbitMqHeaders.CorrelationId], basicProperties.CorrelationId, RabbitMqHeaders.CorrelationId);
        //Assert.AreEqual(headers[RabbitMqHeaders.UserId], basicProperties.UserId, RabbitMqHeaders.UserId);
        //Assert.AreEqual(headers[RabbitMqHeaders.ContentType], basicProperties.ContentType);
        //Assert.AreEqual(headers[RabbitMqHeaders.ContentEncoding], basicProperties.ContentEncoding, RabbitMqHeaders.ContentEncoding);
        //Assert.AreEqual(headers[RabbitMqHeaders.DeliveryMode], basicProperties.DeliveryMode.ToString(), RabbitMqHeaders.DeliveryMode);
        //Assert.AreEqual(headers[RabbitMqHeaders.Type], basicProperties.Type, RabbitMqHeaders.Type);

        //Assert.AreEqual(TimeSpan.FromMinutes(2).TotalMilliseconds.ToString(), basicProperties.Expiration, RabbitMqHeaders.Expiration);
        //Assert.AreEqual(headers[RabbitMqHeaders.Timestamp], ConvertUnixTimeStamp(basicProperties.Timestamp.UnixTime.ToString()).ToString("s"), RabbitMqHeaders.Timestamp);

        //Assert.AreEqual("custom", basicProperties.Headers["Custom-header"], "custom-header");
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

    public static DateTime ConvertUnixTimeStamp(string unixTimeStamp)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(Convert.ToInt64(unixTimeStamp));
    }
}
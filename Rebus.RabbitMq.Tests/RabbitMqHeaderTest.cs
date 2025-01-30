using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable AccessToDisposedClosure

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqHeaderTest : FixtureBase
{
    readonly string _noneExistingQueueName = TestConfig.GetName("non-existing-queue");

    protected override void SetUp()
    {
        RabbitMqTransportFactory.DeleteQueue(_noneExistingQueueName).GetAwaiter().GetResult();
    }

    [Test]
    public async Task Headers()
    {
        using var gotCallback = new ManualResetEvent(false);
        var messageId = Guid.NewGuid();

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
        var userId = GetUsernameFromConnectionString(RabbitMqTransportFactory.ConnectionString);
        var headers = new Dictionary<string, string>
        {
            [RabbitMqHeaders.Mandatory] = bool.TrueString, // set in order to have callback
            [RabbitMqHeaders.MessageId] = messageId.ToString(),
            [RabbitMqHeaders.AppId] = Guid.NewGuid().ToString(),
            [RabbitMqHeaders.CorrelationId] = Guid.NewGuid().ToString(),
            [RabbitMqHeaders.UserId] = userId,
            [RabbitMqHeaders.ContentType] = "text/plain", // NOTE: Gets overridden by JsonSerializer in Rebus
            [RabbitMqHeaders.ContentEncoding] = "none",
            [RabbitMqHeaders.DeliveryMode] = "1",
            [RabbitMqHeaders.Type] = "string",
            [RabbitMqHeaders.Expiration] = TimeSpan.FromMinutes(2).ToString(),
            [RabbitMqHeaders.Timestamp] = timestamp.ToString("s"),
            ["Custom-header"] = "custom",
        };

        var bus = StartOneWayClient(Callback);
        try
        {
            await bus.Advanced.Routing.Send(_noneExistingQueueName, "I have headers", headers);
        }
        catch (RebusApplicationException)
        {
        }

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
        Assert.That(headersFromCallback.GetValueOrDefault(RabbitMqHeaders.UserId), Is.EqualTo(userId));
    }

    static string GetUsernameFromConnectionString(string connectionString)
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo;
        var username = userInfo.Split(":").First();
        return username; 
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
}
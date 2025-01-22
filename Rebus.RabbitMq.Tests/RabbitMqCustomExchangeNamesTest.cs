using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable ArgumentsStyleNamedExpression

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqCustomExchangeNamesTest : FixtureBase
{
    [Test]
    public async Task CanUseCustomExchangeName()
    {
        var connectionString = RabbitMqTransportFactory.ConnectionString;

        const string customDirectExchangeName = "Dingo";
        const string customTopicExchangeName = "Topico";

        await RabbitMqTransportFactory.DeleteExchange(RabbitMqOptionsBuilder.DefaultDirectExchangeName);
        await RabbitMqTransportFactory.DeleteExchange(RabbitMqOptionsBuilder.DefaultTopicExchangeName);
        await RabbitMqTransportFactory.DeleteExchange(customDirectExchangeName);
        await RabbitMqTransportFactory.DeleteExchange(customTopicExchangeName);

        using (var activator = new BuiltinHandlerActivator())
        {
            var gotString = new ManualResetEvent(false);
            activator.Handle<string>(async _ => gotString.Set());

            Configure.With(activator)
                .Transport(t =>
                {
                    var queueName = TestConfig.GetName("custom-exchange");

                    t.UseRabbitMq(connectionString, queueName)
                        .ExchangeNames(directExchangeName: customDirectExchangeName, topicExchangeName: customTopicExchangeName);
                })
                .Start();

            await activator.Bus.SendLocal("hej");

            gotString.WaitOrDie(TimeSpan.FromSeconds(3));
        }

        Assert.That(await RabbitMqTransportFactory.ExchangeExists(RabbitMqOptionsBuilder.DefaultDirectExchangeName), Is.False);
        Assert.That(await RabbitMqTransportFactory.ExchangeExists(RabbitMqOptionsBuilder.DefaultTopicExchangeName), Is.False);
        Assert.That(await RabbitMqTransportFactory.ExchangeExists(customDirectExchangeName), Is.True);
        Assert.That(await RabbitMqTransportFactory.ExchangeExists(customTopicExchangeName), Is.True);
    }
        
    [Test]
    public async Task CanUseAlternateCustomExchangeName()
    {
        var connectionString = RabbitMqTransportFactory.ConnectionString;
            
        var rabbitMqTransport = new RabbitMqTransport(connectionString, "inputQueue", new ConsoleLoggerFactory(false));
        rabbitMqTransport.SetBlockOnReceive(blockOnReceive: false);

        var defaultTopicExchange = "defaultTopicExchange";
        rabbitMqTransport.SetTopicExchangeName(defaultTopicExchange);

        var topic = "myTopic";
        var alternateExchange = "alternateExchange";

        var topicWithAlternateExchange = $"{topic}@{alternateExchange}";

        var subscriberAddresses = await rabbitMqTransport.GetSubscriberAddresses(topicWithAlternateExchange);
        Assert.That(subscriberAddresses[0], Is.EqualTo(topicWithAlternateExchange));
            
        subscriberAddresses = await rabbitMqTransport.GetSubscriberAddresses(topic);
        Assert.That(subscriberAddresses[0], Is.EqualTo($"{topic}@{defaultTopicExchange}"));
            
        subscriberAddresses = await rabbitMqTransport.GetSubscriberAddresses(topic + '@');
        Assert.That(subscriberAddresses[0], Is.EqualTo($"{topic}@"));
    }
}
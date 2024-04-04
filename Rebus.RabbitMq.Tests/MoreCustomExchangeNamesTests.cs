using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class MoreCustomExchangeNamesTests : FixtureBase
{
    string ConnectionString => RabbitMqTestContainerManager.GetConnectionString();

    [Test]
    public async Task CanSendToAlternativeExchange()
    {
        var firstQueueName = TestConfig.GetName("firstQueue");
        var secondQueueName = TestConfig.GetName("secondQueue");

        var firstExchangeName = TestConfig.GetName("firstExchange");
        var secondExchangeName = TestConfig.GetName("secondExchange");

        var (firstActivator, firstStarter) = ConfigureBus(firstQueueName, firstExchangeName);
        var (secondActivator, secondStarter) = ConfigureBus(secondQueueName, secondExchangeName);

        var gotMessageFromSecondExchange = new ManualResetEvent(false);
        var gotMessageFromFirstExchange = new ManualResetEvent(false);

        firstActivator.Handle<string>(async str =>
        {
            if (str == "from second exchange")
            {
                gotMessageFromSecondExchange.Set();
            }
        });

        secondActivator.Handle<string>(async str =>
        {
            if (str == "from first exchange")
            {
                gotMessageFromFirstExchange.Set();
            }
        });

        firstStarter.Start();
        secondStarter.Start();

        await secondActivator.Bus.Advanced.Routing.Send($"{firstQueueName}@{firstExchangeName}Direct", "from second exchange");
        await firstActivator.Bus.Advanced.Routing.Send($"{secondQueueName}@{secondExchangeName}Direct", "from first exchange");

        gotMessageFromFirstExchange.WaitOrDie(TimeSpan.FromSeconds(5));
        gotMessageFromSecondExchange.WaitOrDie(TimeSpan.FromSeconds(5));
    }

    (BuiltinHandlerActivator activator, IBusStarter starter) ConfigureBus(string queueName, string exchangeName)
    {
        var activator = new BuiltinHandlerActivator();

        Using(activator);

        var directExchangeName = $"{exchangeName}Direct";
        var topicExchangeName = $"{exchangeName}Topic";

        Console.WriteLine($@"Configuring bus with

     queue: '{queueName}'
    direct: '{directExchangeName}'
     topic: '{topicExchangeName}'

");

        var starter = Configure.With(activator)
            .Transport(t => t.UseRabbitMq(ConnectionString, queueName)
                .ExchangeNames(directExchangeName, topicExchangeName))
            .Create();

        return (activator, starter);
    }
}
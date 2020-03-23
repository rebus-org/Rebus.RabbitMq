using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleLiteral
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests
{
    [TestFixture]
    public class TestCustomExchange : FixtureBase
    {
        [Test]
        [Description("Subscribing to foreign exchange - this is the way it should be")]
        public async Task CanCommunicateEntirelyViaCustomExchange_Proper()
        {
            var gotTheEvent = new ManualResetEvent(initialState: false);

            var tradingBus = CreateBus("frontoffice", "trading");

            var invoicingBus = CreateBus("backoffice", "invoicing", activator => activator.Handle<TradeRecorded>(async msg => gotTheEvent.Set()));

            await invoicingBus.Advanced.Topics.Subscribe("traderecorded@frontoffice");

            await tradingBus.Advanced.Topics.Publish("traderecorded", new TradeRecorded(Guid.NewGuid()));

            gotTheEvent.WaitOrDie(timeout: TimeSpan.FromSeconds(3));
        }

        [Test]
        [Description("Subscribing to own exchange, publishing to foreign exchange. This is trespassing, because a publisher should normally only publish to its own exchange")]
        public async Task CanCommunicateEntirelyViaCustomExchange_Trespassing()
        {
            var gotTheEvent = new ManualResetEvent(initialState: false);

            var tradingBus = CreateBus("frontoffice", "trading");

            var invoicingBus = CreateBus("backoffice", "invoicing", activator => activator.Handle<TradeRecorded>(async msg => gotTheEvent.Set()));

            await invoicingBus.Advanced.Topics.Subscribe("traderecorded");

            await tradingBus.Advanced.Topics.Publish("traderecorded@backoffice", new TradeRecorded(Guid.NewGuid()));

            gotTheEvent.WaitOrDie(timeout: TimeSpan.FromSeconds(3));
        }

        [Test]
        public async Task CanCommunicateEntirelyViaCustomExchange_EntirelyCustom()
        {
            var gotTheEvent = new ManualResetEvent(initialState: false);

            CreateBus("middleoffice", "confirmations");

            var tradingBus = CreateBus("frontoffice", "trading");

            var invoicingBus = CreateBus("backoffice", "invoicing", activator => activator.Handle<TradeRecorded>(async msg => gotTheEvent.Set()));

            await invoicingBus.Advanced.Topics.Subscribe("traderecorded@middleoffice");

            await tradingBus.Advanced.Topics.Publish("traderecorded@middleoffice", new TradeRecorded(Guid.NewGuid()));

            gotTheEvent.WaitOrDie(timeout: TimeSpan.FromSeconds(3));
        }

        class TradeRecorded
        {
            public Guid TradeId { get; }

            public TradeRecorded(Guid tradeId)
            {
                TradeId = tradeId;
            }
        }

        IBus CreateBus(string exchange, string queueName, Action<BuiltinHandlerActivator> callback = null)
        {
            var activator = Using(new BuiltinHandlerActivator());

            callback?.Invoke(activator);

            Configure.With(activator)
                .Transport(t =>
                {
                    t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, TestConfig.GetName(queueName))
                        .ExchangeNames(topicExchangeName: exchange);
                })
                .Start();

            return activator.Bus;
        }
    }
}
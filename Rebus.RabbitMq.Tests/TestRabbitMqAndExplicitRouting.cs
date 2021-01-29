using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable ArgumentsStyleNamedExpression

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests
{
    [TestFixture]
    public class TestRabbitMqAndExplicitRouting : FixtureBase
    {
        readonly List<ConnectionEndpoint>  _clientConnectionEndpoints = new List<ConnectionEndpoint>
        {
            new ConnectionEndpoint
            {
                ConnectionString =  RabbitMqTransportFactory.ConnectionString
            }
        };
        readonly List<ConnectionEndpoint>  _serverConnectionEndpoints = new List<ConnectionEndpoint>
        {
            new ConnectionEndpoint
            {
                ConnectionString =  RabbitMqTransportFactory.ConnectionString,
                SslSettings = new SslSettings(false, "localhost")
            }
        };

        IBus _bus;

        protected override void SetUp()
        {
            var client = Using(new BuiltinHandlerActivator());

            _bus = Configure.With(client)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseRabbitMqAsOneWayClient(_clientConnectionEndpoints))
                .Options(o => o.SetNumberOfWorkers(0))
                .Start();
        }

        [Test]
        public async Task ReceivesManuallyRoutedMessage()
        {
            var queueName = TestConfig.GetName("manual_routing");
            var gotTheMessage = new ManualResetEvent(false);

            var activator = StartServer(queueName).Handle<string>(async str =>
            {
                gotTheMessage.Set();
            });

            activator.Bus.Advanced.Workers.SetNumberOfWorkers(5);
            Console.WriteLine($"Sending 'hej med dig min ven!' message to '{queueName}'");

            await _bus.Advanced.Routing.Send(queueName, "hej med dig min ven!");

            Console.WriteLine("Waiting for message to arrive");

            gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(50));

            Console.WriteLine("Got it :)");
        }

        BuiltinHandlerActivator StartServer(string queueName)
        {
            var activator = Using(new BuiltinHandlerActivator());

            Configure.With(activator)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseRabbitMq(_serverConnectionEndpoints, queueName))
                .Options(o => o.SetNumberOfWorkers(0))
                .Start();

            return activator;
        }
    }
}
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Serialization;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class ReceiveNonRebusMessageWithRabbitMq : FixtureBase
{
    string ConnectionString => RabbitMqTestContainerManager.GetConnectionString();
    
    readonly string _inputQueueName = TestConfig.GetName("custom-msg");
    BuiltinHandlerActivator _activator;
    IBusStarter _starter;

    protected override void SetUp()
    {
        RabbitMqTransportFactory.DeleteQueue(_inputQueueName).GetAwaiter().GetResult();

        _activator = Using(new BuiltinHandlerActivator());

        _starter = Configure.With(_activator)
            .Logging(l => l.Console(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(ConnectionString, _inputQueueName))
            .Serialization(s => s.Decorate(c => new Utf8Fallback(c.Get<ISerializer>())))
            .Create();
    }

    [Test]
    public async Task CanReceiveNonRebusMessage()
    {
        var receivedCustomStringMessage = new ManualResetEvent(false);

        _activator.Handle<string>(async str =>
        {
            if (str != "hej med dig min ven")
            {
                throw new RebusApplicationException($"Unexpected message: {str}");
            }
            receivedCustomStringMessage.Set();
        });

        _starter.Start();

        await using (var connection = await new ConnectionFactory { Uri = new Uri(ConnectionString) }.CreateConnectionAsync())
        {
            await using (var model = await connection.CreateChannelAsync())
            {
                var body = Encoding.UTF8.GetBytes("hej med dig min ven");
                await model.BasicPublishAsync("RebusDirect", _inputQueueName, body);
            }
        }

        receivedCustomStringMessage.WaitOrDie(TimeSpan.FromSeconds(3));
    }

    class Utf8Fallback : ISerializer
    {
        readonly ISerializer _innerSerializer;

        public Utf8Fallback(ISerializer innerSerializer)
        {
            _innerSerializer = innerSerializer;
        }

        public async Task<TransportMessage> Serialize(Message message)
        {
            return await _innerSerializer.Serialize(message);
        }

        public async Task<Message> Deserialize(TransportMessage transportMessage)
        {
            try
            {
                return await _innerSerializer.Deserialize(transportMessage);
            }
            catch
            {
                var headers = transportMessage.Headers.Clone();
                var body = transportMessage.Body;
                var stringBody = Encoding.UTF8.GetString(body);
                return new Message(headers, stringBody);
            }
        }
    }
}
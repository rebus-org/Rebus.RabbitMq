using System;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.RabbitMq;
using Rebus.Timeouts;
using Rebus.Transport;

namespace Rebus.Config;

/// <summary>
/// Configuration extensions for enabling Rebus' use of RabbitMQ's Delayed Message Exchange plugin.
/// See here: https://github.com/rabbitmq/rabbitmq-delayed-message-exchange
/// </summary>
public static class RabbitMqDelayedMessageExchangeExtensions
{
    /// <summary>
    /// Configures Rebus to use a delayed message exchange with the name <paramref name="exchangeName"/>. The exchange will be automatically declared.
    /// </summary>
    public static void UseDelayedMessageExchange(this StandardConfigurer<ITimeoutManager> configurer, string exchangeName)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (exchangeName == null) throw new ArgumentNullException(nameof(exchangeName));

        configurer
            .OtherService<Options>()
            .Decorate(c =>
            {
                var options = c.Get<Options>();

                var timeoutManagerAddress = $"@{exchangeName}";

                if (!string.IsNullOrWhiteSpace(options.ExternalTimeoutManagerAddressOrNull))
                {
                    throw new RebusConfigurationException($"Cannot set '{timeoutManagerAddress}' as the timeout manager address, because it was already set to '{options.ExternalTimeoutManagerAddressOrNull}'");
                }

                options.ExternalTimeoutManagerAddressOrNull = timeoutManagerAddress;

                // force resolution to have its initializer executed
                c.Get<DelayedMessageExchangeDeclarer>();

                return options;
            });

        configurer.OtherService<ITransport>()
            .Decorate(c => new DelayedMessageExchangeTransportDecorator(c.Get<ITransport>(), c.Get<Options>()));

        configurer
            .OtherService<DelayedMessageExchangeDeclarer>()
            .Register(c => new DelayedMessageExchangeDeclarer(c.Get<IRebusLoggerFactory>(), c.Get<RabbitMqTransport>(), exchangeName, declareExchange: true));
    }

    class DelayedMessageExchangeDeclarer : IInitializable
    {
        readonly RabbitMqTransport _transport;
        readonly string _exchangeName;
        readonly bool _declareExchange;
        readonly ILog _logger;

        public DelayedMessageExchangeDeclarer(IRebusLoggerFactory rebusLoggerFactory, RabbitMqTransport transport, string exchangeName, bool declareExchange)
        {
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _logger = rebusLoggerFactory.GetLogger<DelayedMessageExchangeDeclarer>();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _exchangeName = exchangeName;
            _declareExchange = declareExchange;
        }

        public void Initialize()
        {
            if (!_declareExchange) return;

            _logger.Info("Delaring delayed message exchange with name {exchangeName}", _exchangeName);

            _transport.DeclareDelayedMessageExchange(_exchangeName);
        }
    }

    class DelayedMessageExchangeTransportDecorator : ITransport
    {
        readonly ITransport _transport;
        readonly string _exchangeName;

        public DelayedMessageExchangeTransportDecorator(ITransport transport, Options options)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _exchangeName = options.ExternalTimeoutManagerAddressOrNull;

            if (!_exchangeName.StartsWith("@"))
            {
                throw new ArgumentException(
                    $"Expected the delayed message exchange name to start with '@', e.g. like '@RebusDelayed' or something like that - the value passed to the transport decorator via Options.ExternalTimeoutManagerAddressOrNull was '{options.ExternalTimeoutManagerAddressOrNull}");
            }
        }

        public void CreateQueue(string address) => _transport.CreateQueue(address);

        public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken) => _transport.Receive(context, cancellationToken);

        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            if (message.Headers.TryGetValue(Headers.DeferredRecipient, out var recipient) && message.Headers.TryGetValue(Headers.DeferredUntil, out var deferredUntil))
            {
                message.Headers.Remove(Headers.DeferredUntil);
            }

            return _transport.Send(destinationAddress, message, context);
        }

        public string Address => _transport.Address;
    }
}
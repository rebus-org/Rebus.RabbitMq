using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Extensions;
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
    public static void UseDelayedMessageExchange(this StandardConfigurer<ITimeoutManager> configurer, string exchangeName, bool automaticallyDeclareExchange = true)
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

                return options;
            });

        configurer
            .OtherService<ITransport>()
            .Decorate(c => new DelayedMessageExchangeTransportDecorator(
                rebusLoggerFactory: c.Get<IRebusLoggerFactory>(),
                transport: c.Get<ITransport>(),
                options: c.Get<Options>(),
                rabbitMqTransport: c.Get<RabbitMqTransport>(),
                declareExchange: automaticallyDeclareExchange
            ));
    }

    class DelayedMessageExchangeTransportDecorator : ITransport, IInitializable
    {
        readonly RabbitMqTransport _rabbitMqTransport;
        readonly ITransport _transport;
        readonly string _exchangeName;
        readonly bool _declareExchange;
        readonly ILog _logger;

        public DelayedMessageExchangeTransportDecorator(IRebusLoggerFactory rebusLoggerFactory, ITransport transport, Options options, RabbitMqTransport rabbitMqTransport, bool declareExchange)
        {
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _logger = rebusLoggerFactory.GetLogger<DelayedMessageExchangeTransportDecorator>();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _rabbitMqTransport = rabbitMqTransport ?? throw new ArgumentNullException(nameof(rabbitMqTransport));
            _declareExchange = declareExchange;
            _exchangeName = options.ExternalTimeoutManagerAddressOrNull;

            if (!_exchangeName.StartsWith("@"))
            {
                throw new ArgumentException(
                    $"Expected the delayed message exchange name to start with '@', e.g. like '@RebusDelayed' or something like that - the value passed to the transport decorator via Options.ExternalTimeoutManagerAddressOrNull was '{options.ExternalTimeoutManagerAddressOrNull}");
            }
        }

        public void CreateQueue(string address) => _transport.CreateQueue(address);

        public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken) => _transport.Receive(context, cancellationToken);

        public void Initialize()
        {
            if (!_declareExchange) return;

            var exchangeName = _exchangeName.Substring(1); //< skip the @ prefix

            _logger.Info("Delaring delayed message exchange with name {exchangeName}", exchangeName);

            _rabbitMqTransport.DeclareDelayedMessageExchange(exchangeName).GetAwaiter().GetResult();
        }

        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            var headers = message.Headers;

            if (headers.TryGetValue(Headers.DeferredRecipient, out var recipient) && headers.TryGetValue(Headers.DeferredUntil, out var deferredUntil))
            {
                headers.Remove(Headers.DeferredUntil);
                headers.Remove(Headers.DeferredRecipient);

                var receiveTime = deferredUntil.ToDateTimeOffset();
                var delay = receiveTime - DateTimeOffset.Now;
                var delayMs = (int)delay.TotalMilliseconds;

                headers["x-delay"] = delayMs.ToString(CultureInfo.InvariantCulture);

                return _transport.Send($"{recipient}{_exchangeName}", message, context);
            }

            return _transport.Send(destinationAddress, message, context);
        }

        public string Address => _transport.Address;
    }
}
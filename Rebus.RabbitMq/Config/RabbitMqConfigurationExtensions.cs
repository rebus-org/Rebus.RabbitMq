using System;
using Rebus.Logging;
using Rebus.RabbitMq;
using Rebus.Subscriptions;
using Rebus.Transport;
// ReSharper disable ExpressionIsAlwaysNull

namespace Rebus.Config
{
    /// <summary>
    /// Configuration extensions for the RabbitMQ transport
    /// </summary>
    public static class RabbitMqConfigurationExtensions
    {
        const string RabbitMqSubText = "The RabbitMQ transport was inserted as the subscriptions storage because it has native support for pub/sub messaging";

        /// <summary>
        /// Configures Rebus to use RabbitMQ to transport messages as a one-way client (i.e. will not be able to receive any messages)
        /// </summary>
        public static RabbitMqOptionsBuilder UseRabbitMqAsOneWayClient(this StandardConfigurer<ITransport> configurer, string connectionString)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            var options = new RabbitMqOptionsBuilder();

            configurer
                .OtherService<RabbitMqTransport>()
                .Register(c =>
                {
                    string explicitlyOmittedQueueName = null;
                    var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                    var transport = new RabbitMqTransport(connectionString, explicitlyOmittedQueueName, rebusLoggerFactory);
                    options.Configure(transport);
                    return transport;
                });

            configurer
                .OtherService<ISubscriptionStorage>()
                .Register(c => c.Get<RabbitMqTransport>(), description: RabbitMqSubText);

            configurer.Register(c => c.Get<RabbitMqTransport>());

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);

            return options;
        }

        /// <summary>
        /// Configures Rebus to use RabbitMQ to move messages around
        /// </summary>
        public static RabbitMqOptionsBuilder UseRabbitMq(this StandardConfigurer<ITransport> configurer, string connectionString, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

            var options = new RabbitMqOptionsBuilder();

            configurer
                .OtherService<RabbitMqTransport>()
                .Register(c =>
                {
                    var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                    var transport = new RabbitMqTransport(connectionString, inputQueueName, rebusLoggerFactory);
                    options.Configure(transport);
                    return transport;
                });

            configurer
                .OtherService<ISubscriptionStorage>()
                .Register(c => c.Get<RabbitMqTransport>(), description: RabbitMqSubText);

            configurer.Register(c => c.Get<RabbitMqTransport>());

            return options;
        }
    }
}
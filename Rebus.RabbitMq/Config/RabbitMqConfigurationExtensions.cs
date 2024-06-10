using System;
using System.Collections.Generic;
using Rebus.Injection;
using Rebus.Internals;
using Rebus.Logging;
using Rebus.RabbitMq;
using Rebus.Retry;
using Rebus.Subscriptions;
using Rebus.Transport;
// ReSharper disable ExpressionIsAlwaysNull
// ReSharper disable ArgumentsStyleNamedExpression

namespace Rebus.Config;

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
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

        return BuildInternal(configurer, true, (context, options) => new RabbitMqTransport(connectionString, null, context.Get<IRebusLoggerFactory>(), customizer: options.ConnectionFactoryCustomizer));
    }

    /// <summary>
    /// Configures Rebus to use RabbitMQ to transport messages as a one-way client (i.e. will not be able to receive any messages)
    /// </summary>
    public static RabbitMqOptionsBuilder UseRabbitMqAsOneWayClient(this StandardConfigurer<ITransport> configurer, IList<ConnectionEndpoint> endpoints)
    {
        if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));

        return BuildInternal(configurer, true, (context, options) => new RabbitMqTransport(endpoints, null, context.Get<IRebusLoggerFactory>(), customizer: options.ConnectionFactoryCustomizer));
    }

    /// <summary>
    /// Configures Rebus to use RabbitMQ to move messages around
    /// </summary>
    public static RabbitMqOptionsBuilder UseRabbitMq(this StandardConfigurer<ITransport> configurer, string connectionString, string inputQueueName)
    {
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
        if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

        return BuildInternal(configurer, false, (context, options) => new RabbitMqTransport(connectionString, inputQueueName, context.Get<IRebusLoggerFactory>(), customizer: options.ConnectionFactoryCustomizer));
    }

    /// <summary>
    /// Configures Rebus to use RabbitMQ to move messages around
    /// </summary>
    public static RabbitMqOptionsBuilder UseRabbitMq(this StandardConfigurer<ITransport> configurer, IList<ConnectionEndpoint> endpoints, string inputQueueName)
    {
        if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));
        if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

        return BuildInternal(configurer, false, (context, options) => new RabbitMqTransport(endpoints, inputQueueName, context.Get<IRebusLoggerFactory>(), customizer: options.ConnectionFactoryCustomizer));
    }

    static RabbitMqOptionsBuilder BuildInternal(StandardConfigurer<ITransport> configurer, bool oneway, Func<IResolutionContext, RabbitMqOptionsBuilder, RabbitMqTransport> rabbitMqTransportBuilder)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));

        var options = new RabbitMqOptionsBuilder();

        configurer
            .OtherService<RabbitMqTransport>()
            .Register(c =>
            {
                var transport = rabbitMqTransportBuilder(c, options);
                options.Configure(transport);
                return transport;
            });

        configurer
            .OtherService<ISubscriptionStorage>()
            .Register(c => c.Get<RabbitMqTransport>(), description: RabbitMqSubText);

        configurer.Register(c => c.Get<RabbitMqTransport>());

        if (oneway)
        {
            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }
        else
        {
            // ensure that the x-delivery-count header is cleared when messages are moved to the error queue
            configurer.OtherService<IErrorHandler>()
                .Decorate(c => new RabbitMqErrorHandlerDecorator(c.Get<IErrorHandler>()));
        }

        return options;
    }
}
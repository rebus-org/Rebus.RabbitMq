using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Rebus.RabbitMq;
// ReSharper disable UnusedMember.Global

namespace Rebus.Config;

/// <summary>
/// Allows for fluently configuring RabbitMQ options
/// </summary>
public class RabbitMqOptionsBuilder
{
    readonly Dictionary<string, string> _additionalClientProperties = new();

    /// <summary>
    /// Default name of the exchange of type DIRECT (used for point-to-point messaging)
    /// </summary>
    public const string DefaultDirectExchangeName = "RebusDirect";

    /// <summary>
    /// Default name of the exchange of type TOPIC (used for pub-sub)
    /// </summary>
    public const string DefaultTopicExchangeName = "RebusTopics";

    /// <summary>
    /// Configures which things to auto-declare and whether to bind the input queue. 
    /// Please note that you must be careful when you skip e.g. binding of the input queue as it may lead to lost messages
    /// if the direct binding is not established. 
    /// By default, two exchanges will be declared: one of the DIRECT type (for point-to-point messaging) and one of the
    /// TOPIC type (for pub-sub). Moreover, the endpoint's input queue will be declared, and a binding
    /// will be made from a topic of the same name as the input queue in the DIRECT exchange.
    /// </summary>
    public RabbitMqOptionsBuilder Declarations(bool declareExchanges = true, bool declareInputQueue = true, bool bindInputQueue = true)
    {
        DeclareExchanges = declareExchanges;
        DeclareInputQueue = declareInputQueue;
        BindInputQueue = bindInputQueue;
        return this;
    }

    /// <summary>
    /// Registers a callback, which may be used to customize - or completely replace - the connection factory
    /// used by Rebus' RabbitMQ transport
    /// </summary>
    public RabbitMqOptionsBuilder CustomizeConnectionFactory(Func<IConnectionFactory, IConnectionFactory> customizer)
    {
        if (ConnectionFactoryCustomizer != null)
        {
            throw new InvalidOperationException("Attempted to register a connection factory customization function, but one has already been registered");
        }

        ConnectionFactoryCustomizer = customizer ?? throw new ArgumentNullException(nameof(customizer), "Please provide a connection factory customizer function when calling this method");

        return this;
    }

    /// <summary>
    /// Sets max number of messages to prefetch
    /// </summary>
    public RabbitMqOptionsBuilder Prefetch(int maxNumberOfMessagesToPrefetch)
    {
        if (maxNumberOfMessagesToPrefetch <= 0)
        {
            throw new ArgumentException($"Cannot set 'max messages to prefetch' to {maxNumberOfMessagesToPrefetch} - it must be at least 1!");
        }

        MaxNumberOfMessagesToPrefetch = maxNumberOfMessagesToPrefetch;
        return this;
    }

    /// <summary>
    /// Configures which names to use for the two types of necessary exchanges
    /// </summary>
    public RabbitMqOptionsBuilder ExchangeNames(
        string directExchangeName = DefaultDirectExchangeName,
        string topicExchangeName = DefaultTopicExchangeName)
    {
        if (directExchangeName == null) throw new ArgumentNullException(nameof(directExchangeName));
        if (topicExchangeName == null) throw new ArgumentNullException(nameof(topicExchangeName));

        if (directExchangeName == topicExchangeName)
        {
            throw new ArgumentException($"Exchange names for DIRECT and TOPIC are both set to '{directExchangeName}' - they must be different!");
        }

        DirectExchangeName = directExchangeName;
        TopicExchangeName = topicExchangeName;

        return this;
    }

    /// <summary>
    /// Adds the given custom properties to be added to the RabbitMQ client connection when it is established
    /// </summary>
    public RabbitMqOptionsBuilder AddClientProperties(IDictionary<string, string> additionalProperties)
    {
        foreach (var kvp in additionalProperties)
        {
            _additionalClientProperties[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Configure input queue as a strict priority queue. 
    /// This setting adds "x-max-priority" argument to the input queue parameters
    /// And sets Prefetch(1) in order to strictly prioritize messages
    /// </summary>
    public RabbitMqOptionsBuilder StrictPriorityQueue(int maxPriority)
    {
        PriorityQueue(maxPriority);
        Prefetch(1);

        return this;
    }

    /// <summary>
    /// Configure input queue as a priority queue. 
    /// </summary>
    public RabbitMqOptionsBuilder PriorityQueue(int maxPriority)
    {
        InputQueueOptionsBuilder.Arguments.Add("x-max-priority", maxPriority);

        return this;
    }

    /// <summary>
    /// Configure mandatory delivery. 
    /// This configuration tells the server how to react if the message cannot be routed to a queue. 
    /// If this configuration is set, the server will return an unroutable message with a Return method. 
    /// If this configuration is not used, the server silently drops the message
    /// </summary>
    public RabbitMqOptionsBuilder Mandatory(Action<object, BasicReturnEventArgs> basicReturnCallback)
    {
        if (basicReturnCallback == null)
        {
            return this;
        }
        return Mandatory((o, e) =>
        {
            basicReturnCallback(o, e);
            return Task.CompletedTask;
        });
    }
    
    /// <summary>
    /// Configure mandatory delivery. 
    /// This configuration tells the server how to react if the message cannot be routed to a queue. 
    /// If this configuration is set, the server will return an unroutable message with a Return method. 
    /// If this configuration is not used, the server silently drops the message
    /// </summary>
    public RabbitMqOptionsBuilder Mandatory(Func<object, BasicReturnEventArgs, Task> basicReturnCallback)
    {
        if (basicReturnCallback == null)
        {
            return this;
        }
        
        CallbackOptionsBuilder.BasicReturn((o, e) =>
        {
            basicReturnCallback(o, e);
            return Task.CompletedTask;
        });

        return this;
    }

    /// <summary>
    /// Configure input queue specifically. Beware that this will override default settings.
    /// If used in conjunction with PriorityQueue and StrictPriorityQueue options it might have unexpected results. 
    /// </summary>
    public RabbitMqOptionsBuilder InputQueueOptions(Action<RabbitMqQueueOptionsBuilder> configurer)
    {
        configurer?.Invoke(InputQueueOptionsBuilder);

        return this;
    }

    /// <summary>
    /// Configure default queue options manually. Beware that this will override default settings.
    /// If used in conjunction with PriorityQueue and StrictPriorityQueue options it might have unexpected results. 
    /// </summary>
    public RabbitMqOptionsBuilder DefaultQueueOptions(Action<RabbitMqQueueOptionsBuilder> configurer)
    {
        configurer?.Invoke(DefaultQueueOptionsBuilder);

        return this;
    }

    /// <summary>
    /// Configure input exchanges manually. 
    /// </summary>
    public RabbitMqOptionsBuilder InputExchangeOptions(Action<RabbitMqExchangeOptionsBuilder> configurer)
    {
        configurer?.Invoke(ExchangeOptions);

        return this;
    }

    /// <summary>
    /// Register RabbitMq callback events. Events are triggered dependening on the message headers.
    /// </summary>
    public RabbitMqOptionsBuilder RegisterEventCallbacks(Action<RabbitMqCallbackOptionsBuilder> configurer)
    {
        configurer?.Invoke(CallbackOptionsBuilder);

        return this;
    }

    /// <summary>
    /// Sets SLL settings to use when connecting to the broker
    /// This method is intended to use only when constructing RabbitMq Transport with single node provided through string connectionString
    /// </summary>
    public RabbitMqOptionsBuilder Ssl(SslSettings sslSettings)
    {
        SslSettings = sslSettings;
        return this;
    }

    /// <summary>
    /// Set whether the publisher confirms protocol is enabled. To avoid message loss, publisher confirms ARE ENABLED BY DEFAULT.
    /// Please note that you can opt out of publisher confirms ON A PER-MESSAGE BASIS by adding the <see cref="Messages.Headers.Express"/>
    /// header to a message.
    /// Calling this method with <paramref name="enabled"/> = false will disable publisher confirms altogether.
    /// The messages of a transaction are published concurrently and their confirms awaited together, so the order
    /// in which they reach the broker is NOT guaranteed.
    /// </summary>
    public RabbitMqOptionsBuilder SetPublisherConfirms(bool enabled)
    {
        PublisherConfirmsEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Set whether the publisher confirms protocol is enabled. To avoid message loss, publisher confirms ARE ENABLED BY DEFAULT.
    /// Please note that you can opt out of publisher confirms ON A PER-MESSAGE BASIS by adding the <see cref="Messages.Headers.Express"/>
    /// header to a message.
    /// Calling this method with <paramref name="enabled"/> = false will disable publisher confirms altogether.
    /// The messages of a transaction are published concurrently and their confirms awaited together, so the order
    /// in which they reach the broker is NOT guaranteed.
    /// </summary>
    /// <param name="enabled">Whether to use publisher confirms.</param>
    /// <param name="timeout">How long to wait for the broker to confirm the messages of a transaction before failing it. Use <code>TimeSpan.Zero</code> for INFINITE timeout.</param>
    public RabbitMqOptionsBuilder SetPublisherConfirms(bool enabled, TimeSpan timeout)
    {
        PublisherConfirmsEnabled = enabled;
        PublisherConfirmsTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Set the connection_name property (user-friendly non-unique client connection name) of RabbitMQ connection, which is 
    /// shown in the connections overview list and in the client properites of a connection.         
    /// </summary>
    public RabbitMqOptionsBuilder SetConnectionName(string connectionName)
    {
        ConnectionName = connectionName;
        return this;
    }

    /// <summary>
    /// Sets the consumer tag. The actual tag will include a random string to guarantee uniqueness. 
    /// </summary>
    public RabbitMqOptionsBuilder SetConsumerTag(string consumerTag)
    {
        ConsumerTag = consumerTag;
        return this;
    }
  
    internal bool? DeclareExchanges { get; private set; }
    internal bool? DeclareInputQueue { get; private set; }
    internal bool? BindInputQueue { get; private set; }
    internal bool? PublisherConfirmsEnabled { get; private set; }
    internal TimeSpan? PublisherConfirmsTimeout { get; private set; }

    internal string DirectExchangeName { get; private set; }
    internal string TopicExchangeName { get; private set; }
    
    internal string ConsumerTag { get; private set; }
    internal string ConnectionName { get; private set; }

    internal int? MaxNumberOfMessagesToPrefetch { get; private set; }

    internal SslSettings SslSettings { get; private set; }

    internal RabbitMqCallbackOptionsBuilder CallbackOptionsBuilder { get; } = new();

    internal RabbitMqQueueOptionsBuilder InputQueueOptionsBuilder { get; } = new();

    internal RabbitMqQueueOptionsBuilder DefaultQueueOptionsBuilder { get; } = new();

    internal RabbitMqExchangeOptionsBuilder ExchangeOptions { get; } = new();

    internal Func<IConnectionFactory, IConnectionFactory> ConnectionFactoryCustomizer;

    internal void Configure(RabbitMqTransport transport)
    {
        transport.AddClientProperties(_additionalClientProperties);

        if (SslSettings != null)
        {
            transport.SetSslSettings(SslSettings);
        }

        if (DeclareExchanges.HasValue)
        {
            transport.SetDeclareExchanges(DeclareExchanges.Value);
        }

        if (DeclareInputQueue.HasValue)
        {
            transport.SetDeclareInputQueue(DeclareInputQueue.Value);
        }

        if (BindInputQueue.HasValue)
        {
            transport.SetBindInputQueue(BindInputQueue.Value);
        }

        if (DirectExchangeName != null)
        {
            transport.SetDirectExchangeName(DirectExchangeName);
        }

        if (TopicExchangeName != null)
        {
            transport.SetTopicExchangeName(TopicExchangeName);
        }

        if (MaxNumberOfMessagesToPrefetch != null)
        {
            transport.SetMaxMessagesToPrefetch(MaxNumberOfMessagesToPrefetch.Value);
        }

        if (CallbackOptionsBuilder != null)
        {
            transport.SetCallbackOptions(CallbackOptionsBuilder);
        }

        if (PublisherConfirmsEnabled.HasValue)
        {
            var timeout = PublisherConfirmsTimeout ?? TimeSpan.FromSeconds(60);

            transport.EnablePublisherConfirms(PublisherConfirmsEnabled.Value, timeout);
        }

        transport.SetInputQueueOptions(InputQueueOptionsBuilder);
        transport.SetDefaultQueueOptions(DefaultQueueOptionsBuilder);
        transport.SetExchangeOptions(ExchangeOptions);
        transport.SetConsumerTag(ConsumerTag);
    }
}
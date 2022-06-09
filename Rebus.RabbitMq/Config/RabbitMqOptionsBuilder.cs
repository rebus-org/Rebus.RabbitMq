﻿using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using Rebus.RabbitMq;

namespace Rebus.Config;

/// <summary>
/// Allows for fluently configuring RabbitMQ options
/// </summary>
public class RabbitMqOptionsBuilder
{
    readonly Dictionary<string, string> _additionalClientProperties = new Dictionary<string, string>();

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

        ConnectionFactoryCustomizer = customizer ?? throw new ArgumentNullException(nameof(customizer));

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
        CallbackOptionsBuilder.BasicReturn(basicReturnCallback);

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
    /// Calling this method with <paramref name="enabled"/> = false will disable publisher confirms alltogether.
    /// </summary>
    public RabbitMqOptionsBuilder SetPublisherConfirms(bool enabled)
    {
        PublisherConfirmsEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Set the connection_name property (user-friendly non-unique client connection name) of RabbitMQ connection, which is 
    /// shown in the connections overview list and in the client properites of a connection.         
    /// </summary>
    /// <exception cref="InvalidOperationException">expcetion is thrown if another connection factory customizer is in use</exception>
    public RabbitMqOptionsBuilder ClientConnectionName(string connectionName)
    {
        return CustomizeConnectionFactory(factory => new ConnectionFactoryClientNameDecorator(factory, connectionName));
    }

    /// <summary>
    /// Sets the max amount of writers that are available kept around for writing messages back
    /// to rabbitmq. Reducing this number uses less resource, while increasing it might increase
    /// performance on high-rate systems. We would recommend at least as many as you have
    /// MaxParallel set to, and probably a bit more if you send messages from a webapi through
    /// Rebus.
    /// </summary>
    public RabbitMqOptionsBuilder SetMaxWriterPoolSize(int size)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "MaxWriterPoolSize cannot be less than 1");
        }

        MaxWriterPoolSize = size;
        return this;
    }

    internal bool? DeclareExchanges { get; private set; }
    internal bool? DeclareInputQueue { get; private set; }
    internal bool? BindInputQueue { get; private set; }
    internal bool? PublisherConfirmsEnabled { get; private set; }

    internal string DirectExchangeName { get; private set; }
    internal string TopicExchangeName { get; private set; }

    internal int? MaxNumberOfMessagesToPrefetch { get; private set; }

    internal SslSettings SslSettings { get; private set; }

    internal RabbitMqCallbackOptionsBuilder CallbackOptionsBuilder { get; } = new RabbitMqCallbackOptionsBuilder();

    internal RabbitMqQueueOptionsBuilder InputQueueOptionsBuilder { get; } = new RabbitMqQueueOptionsBuilder();
        
    internal RabbitMqQueueOptionsBuilder DefaultQueueOptionsBuilder { get; } = new RabbitMqQueueOptionsBuilder();

    internal RabbitMqExchangeOptionsBuilder ExchangeOptions { get; } = new RabbitMqExchangeOptionsBuilder();
        
    internal int MaxWriterPoolSize { get; private set; } = 10;

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
            transport.EnablePublisherConfirms(PublisherConfirmsEnabled.Value);
        }

        transport.SetInputQueueOptions(InputQueueOptionsBuilder);
        transport.SetDefaultQueueOptions(DefaultQueueOptionsBuilder);
        transport.SetExchangeOptions(ExchangeOptions);
        transport.SetMaxWriterPoolSize(MaxWriterPoolSize);
    }

    /// This is temporary decorator-fix, until Rebus is upgraded to a version 6+ of RabbitMQ.Client wich has new signature:
    /// IConnection CreateConnection(IList AmqpTcpEndpoint endpoints, string clientProvidedName) 
    /// so it is more correct to provide the name of client connection in ConnectionManager.GetConnection() method, when connections are created.
    class ConnectionFactoryClientNameDecorator : IConnectionFactory
    {
        private readonly IConnectionFactory _decoratedFactory;
        private readonly string _clientProvidedName;

        public IDictionary<string, object> ClientProperties
        {
            get { return _decoratedFactory.ClientProperties; }
            set { _decoratedFactory.ClientProperties = value; }
        }

        public TimeSpan ContinuationTimeout
        {
            get { return _decoratedFactory.ContinuationTimeout; }
            set { _decoratedFactory.ContinuationTimeout = value; }
        }

        public TimeSpan HandshakeContinuationTimeout
        {
            get { return _decoratedFactory.HandshakeContinuationTimeout; }
            set { _decoratedFactory.HandshakeContinuationTimeout = value; }
        }

        public string Password
        {
            get { return _decoratedFactory.Password; }
            set { _decoratedFactory.Password = value; }
        }

        public ushort RequestedChannelMax
        {
            get { return _decoratedFactory.RequestedChannelMax; }
            set { _decoratedFactory.RequestedChannelMax = value; }
        }

        public uint RequestedFrameMax
        {
            get { return _decoratedFactory.RequestedFrameMax; }
            set { _decoratedFactory.RequestedFrameMax = value; }
        }

        public TimeSpan RequestedHeartbeat
        {
            get { return _decoratedFactory.RequestedHeartbeat; }
            set { _decoratedFactory.RequestedHeartbeat = value; }
        }

        public Uri Uri
        {
            get { return _decoratedFactory.Uri; }
            set { _decoratedFactory.Uri = value; }
        }

        public bool UseBackgroundThreadsForIO
        {
            get { return _decoratedFactory.UseBackgroundThreadsForIO; }
            set { _decoratedFactory.UseBackgroundThreadsForIO = value; }
        }

        public string UserName
        {
            get { return _decoratedFactory.UserName; }
            set { _decoratedFactory.UserName = value; }
        }

        public string VirtualHost
        {
            get { return _decoratedFactory.VirtualHost; }
            set { _decoratedFactory.VirtualHost = value; }
        }

        public string ClientProvidedName 
        { 
            get { return _decoratedFactory.ClientProvidedName; }
            set { _decoratedFactory.ClientProvidedName = value; } 
        }

        public ConnectionFactoryClientNameDecorator(IConnectionFactory originalFactory, string clientProvidedName)
        {
            _decoratedFactory = originalFactory;
            _clientProvidedName = clientProvidedName;
        }

        public IAuthMechanismFactory AuthMechanismFactory(IList<string> mechanismNames)
        {
            return _decoratedFactory.AuthMechanismFactory(mechanismNames);
        }

        public IConnection CreateConnection(IList<AmqpTcpEndpoint> endpoints)
        {
            return (_decoratedFactory as RabbitMQ.Client.ConnectionFactory).CreateConnection(new DefaultEndpointResolver(endpoints), _clientProvidedName);
        }

        public IConnection CreateConnection()
        {
            return _decoratedFactory.CreateConnection(_clientProvidedName);
        }

        public IConnection CreateConnection(string clientProvidedName)
        {
            return _decoratedFactory.CreateConnection(clientProvidedName);
        }

        public IConnection CreateConnection(IList<string> hostnames)
        {
            return _decoratedFactory.CreateConnection(hostnames, _clientProvidedName);
        }

        public IConnection CreateConnection(IList<string> hostnames, string clientProvidedName)
        {
            return _decoratedFactory.CreateConnection(hostnames, clientProvidedName);
        }

        public IConnection CreateConnection(IList<AmqpTcpEndpoint> endpoints, string clientProvidedName)
        {
            return (_decoratedFactory as RabbitMQ.Client.ConnectionFactory).CreateConnection(new DefaultEndpointResolver(endpoints), clientProvidedName);
        }
    }
}
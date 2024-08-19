using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Subscriptions;
using Rebus.Transport;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Internals;
using Headers = Rebus.Messages.Headers;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable ArgumentsStyleLiteral

#pragma warning disable 1998

namespace Rebus.RabbitMq;

/// <summary>
/// Implementation of <see cref="ITransport"/> that uses RabbitMQ to send/receive messages
/// </summary>
public class RabbitMqTransport : AbstractRebusTransport, IDisposable, IInitializable, ISubscriptionStorage
{
    /// <summary>
    /// <see cref="ShutdownEventArgs.ReplyCode"/> value that indicates that a queue does not exist
    /// </summary>
    const int QueueDoesNotExist = 404;

    /// <summary>
    /// Defines how many attempts to make at sending outgoung messages before letting exceptions bubble out
    /// </summary>
    const int WriterAttempts = 3;

    static readonly Encoding HeaderValueEncoding = Encoding.UTF8;

    readonly SemaphoreSlim _consumerLock = new(1, 1);
    readonly ModelObjectPool _writerPool;

    CustomQueueingConsumer _consumer;

    readonly ConcurrentDictionary<FullyQualifiedRoutingKey, bool> _verifiedQueues = new();

    readonly List<Subscription> _registeredSubscriptions = new();
    readonly SemaphoreSlim _subscriptionSemaphore = new(1, 1);
    readonly ConnectionManager _connectionManager;
    readonly ILog _log;
    
    ushort _maxMessagesToPrefetch;

    bool _declareExchanges = true;
    bool _declareInputQueue = true;
    bool _bindInputQueue = true;
    bool _publisherConfirmsEnabled = true;
    int _batchSize = 1;
    
    TimeSpan _publisherConfirmsTimeout;

    string _consumerTag = null;

    string _directExchangeName = RabbitMqOptionsBuilder.DefaultDirectExchangeName;
    string _topicExchangeName = RabbitMqOptionsBuilder.DefaultTopicExchangeName;

    RabbitMqCallbackOptionsBuilder _callbackOptions = new();
    RabbitMqQueueOptionsBuilder _inputQueueOptions = new();
    RabbitMqQueueOptionsBuilder _defaultQueueOptions = new();
    RabbitMqExchangeOptionsBuilder _inputExchangeOptions = new();

    long _queueDeclarePassiveCounter;

    RabbitMqTransport(IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch, string inputQueueAddress) :
        base(inputQueueAddress)
    {
        if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
        if (maxMessagesToPrefetch <= 0)
        {
            throw new ArgumentException($"Cannot set 'maxMessagesToPrefetch' to {maxMessagesToPrefetch} - it must be at least 1!");
        }

        _maxMessagesToPrefetch = (ushort)maxMessagesToPrefetch;

        _log = rebusLoggerFactory.GetLogger<RabbitMqTransport>();

        _writerPool = new ModelObjectPool(new WriterModelPoolPolicy(this), 5);
    }

    /// <summary>
    /// Constructs the RabbitMQ transport with multiple connection endpoints. They will be tried in random order until working one is found
    ///  Credentials will be extracted from the connectionString of the first provided endpoint
    /// </summary>
    public RabbitMqTransport(IList<ConnectionEndpoint> endpoints, string inputQueueAddress,
        IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch = 50,
        Func<IConnectionFactory, IConnectionFactory> customizer = null)
        : this(rebusLoggerFactory, maxMessagesToPrefetch, inputQueueAddress)
    {
        if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));

        _connectionManager = new ConnectionManager(endpoints, inputQueueAddress, rebusLoggerFactory, customizer);
    }

    /// <summary>
    /// Constructs the transport with a connection to the RabbitMQ instance specified by the given connection string.
    /// Multiple connection strings could be provided. They should be separates with , or ; 
    /// </summary>
    public RabbitMqTransport(string connectionString, string inputQueueAddress,
        IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch = 50,
        Func<IConnectionFactory, IConnectionFactory> customizer = null)
        : this(rebusLoggerFactory, maxMessagesToPrefetch, inputQueueAddress)
    {
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

        _connectionManager =
            new ConnectionManager(connectionString, inputQueueAddress, rebusLoggerFactory, customizer);
    }

    public void SetBlockOnReceive(bool blockOnReceive) => _blockOnReceive = blockOnReceive;

    /// <summary>
    /// Stores the client properties to be handed to RabbitMQ when the connection is established
    /// </summary>
    public void AddClientProperties(Dictionary<string, string> additionalClientProperties)
    {
        _connectionManager.AddClientProperties(additionalClientProperties);
    }

    /// <summary>
    /// Stores ssl options to be used when connection to RabbitMQ is established
    /// intended to use with single node Rabbitmq setup
    /// </summary>
    public void SetSslSettings(SslSettings sslSettings)
    {
        _connectionManager.SetSslOptions(sslSettings);
    }

    /// <summary>
    /// Sets the maximum message batch size. Defaults to 1 (i.e. batching is disabled).
    /// </summary>
    public void SetBatchSize(int batchSize)
    {
        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Message batch size cannot be less than 1");
        }
        _batchSize = batchSize;
    }

    /// <summary>
    /// Sets whether the exchange should be declared
    /// </summary>
    public void SetDeclareExchanges(bool value)
    {
        _declareExchanges = value;
    }

    /// <summary>
    /// Sets whether the endpoint's input queue should be declared
    /// </summary>
    public void SetDeclareInputQueue(bool value)
    {
        _declareInputQueue = value;
    }

    /// <summary>
    /// Sets whether a binding for the input queue should be declared
    /// </summary>
    public void SetBindInputQueue(bool value)
    {
        _bindInputQueue = value;
    }

    /// <summary>
    /// Sets whether to use the publisher confirms protocol. When <paramref name="value"/> is set to true,
    /// the <paramref name="timeout"/> parameter indicates how long to wait for the confirmation from the broker. Use <code>TimeSpan.Zero</code>
    /// for INFINITE timeout.
    /// </summary>
    public void EnablePublisherConfirms(bool value, TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException($"Cannot use publisher confirms timeout value {timeout} - the value must be a positive TimeSpan, or TimeSpan.Zero for inifinite timeout");
        }

        _publisherConfirmsEnabled = value;
        _publisherConfirmsTimeout = timeout;
    }

    /// <summary>
    /// Sets the name of the exchange used to send point-to-point messages
    /// </summary>
    public void SetDirectExchangeName(string directExchangeName)
    {
        _directExchangeName = directExchangeName;
    }

    /// <summary>
    /// Sets the name of the exchange used to do publish/subscribe messaging
    /// </summary>
    public void SetTopicExchangeName(string topicExchangeName)
    {
        _topicExchangeName = topicExchangeName;
    }

    /// <summary>
    /// Configures how many messages to prefetch
    /// </summary>
    public void SetMaxMessagesToPrefetch(int maxMessagesToPrefetch)
    {
        if (maxMessagesToPrefetch < 0)
        {
            throw new ArgumentException($"Cannot set 'max messages to prefetch' to {maxMessagesToPrefetch}");
        }

        _maxMessagesToPrefetch = (ushort)maxMessagesToPrefetch;
    }

    /// <summary>
    /// Configures BasicModel events
    /// </summary>
    public void SetCallbackOptions(RabbitMqCallbackOptionsBuilder callbackOptions)
    {
        _callbackOptions = callbackOptions ?? throw new ArgumentNullException(nameof(callbackOptions));
    }

    /// <summary>
    /// Configures input queue options
    /// </summary>
    public void SetInputQueueOptions(RabbitMqQueueOptionsBuilder inputQueueOptions)
    {
        _inputQueueOptions = inputQueueOptions ?? throw new ArgumentNullException(nameof(inputQueueOptions));
    }

    /// <summary>
    /// Configures input queue options
    /// </summary>
    public void SetDefaultQueueOptions(RabbitMqQueueOptionsBuilder defaultQueueOptions)
    {
        _defaultQueueOptions = defaultQueueOptions ?? throw new ArgumentNullException(nameof(defaultQueueOptions));
    }

    /// <summary>
    /// Configures input exchange options
    /// </summary>
    public void SetExchangeOptions(RabbitMqExchangeOptionsBuilder inputExchangeOptions)
    {
        _inputExchangeOptions =
            inputExchangeOptions ?? throw new ArgumentNullException(nameof(inputExchangeOptions));
    }

    /// <summary>
    /// Sets the max amount of writers that are available kept around for writing messages back
    /// to rabbitmq. Reducing this number uses less resource, while increasing it might increase
    /// performance on high-rate systems. 
    /// </summary>
    public void SetMaxWriterPoolSize(int poolSize)
    {
        _writerPool.SetMaxEntries(poolSize);
    }

    public void SetConsumerTag(string consumerTag)
    {
        _consumerTag = consumerTag;
    }

    /// <summary>
    /// Initializes the transport by creating the input queue
    /// </summary>
    public void Initialize()
    {
        if (Address == null) { return; }

        CreateQueue(Address);
    }

    /// <summary>
    /// Creates a queue with the given name and binds it to a topic with the same name in the direct exchange
    /// </summary>
    public override void CreateQueue(string address)
    {
        // bail out without creating a connection if there's no need for it
        if (!_declareExchanges && !_declareInputQueue && !_bindInputQueue) return;

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(delay: TimeSpan.FromSeconds(60));

            while (true)
            {
                try
                {
                    var connection = _connectionManager.GetConnection();

                    using var model = connection.CreateModel();

                    const bool durable = true;

                    if (_declareExchanges)
                    {
                        DeclareExchanges(model, durable);
                    }

                    if (_declareInputQueue)
                    {
                        DeclareQueue(address, model);
                    }

                    if (_bindInputQueue)
                    {
                        BindInputQueue(address, model);
                    }

                    model.Close();
                    return;
                }
                catch (Exception) when (!cancellationTokenSource.IsCancellationRequested)
                {
                    // keep trying a couple of times
                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception exception)
        {
            throw new RebusApplicationException(exception, $"Queue declaration for '{address}' failed");
        }
    }

    public void DeclareDelayedMessageExchange(string exchangeName)
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(delay: TimeSpan.FromSeconds(60));

            while (true)
            {
                try
                {
                    var connection = _connectionManager.GetConnection();

                    using var model = connection.CreateModel();

                    model.ExchangeDeclare(
                        exchange: exchangeName,
                        type: "x-delayed-message",
                        durable: true,
                        autoDelete: false,
                        arguments: new Dictionary<string, object> { ["x-delayed-type"] = "direct" }
                    );

                    model.Close();
                    return;
                }
                catch (Exception) when (!cancellationTokenSource.IsCancellationRequested)
                {
                    // keep trying a couple of times
                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception exception)
        {
            throw new RebusApplicationException(exception, $"Delayed message exchange declaration for '{exchangeName}' failed");
        }
    }

    void DeclareExchanges(IModel model, bool durable)
    {
        model.ExchangeDeclare(
            exchange: _directExchangeName,
            type: ExchangeType.Direct,
            durable: durable,
            arguments: _inputExchangeOptions.DirectExchangeArguments
        );
        model.ExchangeDeclare(
            exchange: _topicExchangeName,
            type: ExchangeType.Topic,
            durable: durable,
            arguments: _inputExchangeOptions.TopicExchangeArguments
        );
    }

    void DeclareQueue(string address, IModel model)
    {
        if (Equals(address, Address))
        {
            // This is the input queue => we use the queue setting to create the queue
            model.QueueDeclare(
                queue: address,
                exclusive: _inputQueueOptions.Exclusive,
                durable: _inputQueueOptions.Durable,
                autoDelete: _inputQueueOptions.AutoDelete,
                arguments: _inputQueueOptions.Arguments
            );
        }
        else
        {
            // This is another queue, probably the error queue => we use the default queue options
            model.QueueDeclare(
                queue: address,
                exclusive: _defaultQueueOptions.Exclusive,
                durable: _defaultQueueOptions.Durable,
                autoDelete: _defaultQueueOptions.AutoDelete,
                arguments: _defaultQueueOptions.Arguments
            );
        }
    }

    void BindInputQueue(string address, IModel model)
    {
        model.QueueBind(address, _directExchangeName, address);
    }


    /// <inheritdoc />
    protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
    {
        var messages = outgoingMessages
            .Select(m => new { Message = m, IsExpress = m.TransportMessage.Headers.ContainsKey(Headers.Express) })
            .ToList();

        if (!messages.Any()) return;

        var expressMessages = messages.Where(m => m.IsExpress).Select(m => m.Message).ToList();
        var ordinaryMessages = messages.Where(m => !m.IsExpress).Select(m => m.Message).ToList();

        var attempt = 0;

        while (true)
        {
            var model = _writerPool.Get();

            try
            {
                // if the model is not fit, discard it and try a new one
                if (model.IsClosed)
                {
                    model.SafeDrop();
                    continue;
                }

                // otherwise, count this as an attempt and try to do it
                attempt++;

                DoSend(expressMessages, model, isExpress: true);
                DoSend(ordinaryMessages, model, isExpress: false);
                
                // remember to return the model
                _writerPool.Return(model);
                
                return; //< success - we're done!
            }
            catch (Exception)
            {
                // if anything goes wrong when using this IModel, asummed it's faulted and drop it
                model.SafeDrop();

                // if the built-in number of retries has been exceeded, let the error bubble out
                if (attempt > WriterAttempts)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Deletes all messages from the queue
    /// </summary>
    public void PurgeInputQueue()
    {
        var connection = _connectionManager.GetConnection();

        using var model = connection.CreateModel();

        try
        {
            model.QueuePurge(Address);
        }
        catch (OperationInterruptedException exception) when (exception.HasReplyCode(QueueDoesNotExist))
        {
            // ignore this error if the queue does not exist
        }
    }

    bool _blockOnReceive = true;

    /// <inheritdoc />
    public override async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
    {
        if (Address == null)
        {
            throw new InvalidOperationException("This RabbitMQ transport does not have an input queue - therefore, it is not possible to receive anything");
        }

        try
        {
            if (_consumer == null)
            {
                await _consumerLock.WaitAsync(cancellationToken);
                try
                {
                    _consumer ??= InitializeConsumer();
                }
                finally
                {
                    _consumerLock.Release();
                }
            }

            try
            {
                // When a consumer is dequeued from the the "consumers" pool, it might be bound to a queue, which does not exist anymore,
                // eg. expired and deleted by RabittMQ server policy). In this case this calling QueueDeclarePassive will result in 
                // an OperationInterruptedException and "consumer.Model.IsOpen" will be set to false (this is handled later in the code by 
                // disposing this consumer). There is no need to handle this exception. The logic of InitializeConsumer() will make sure 
                // that the queue is recreated later based on assumption about how ReBus is handling null-result of ITransport.Receive().
                // Applied this logic to recreate the queue only if the transport is configure initially to create the queue if it doesnt exists.
                if (_declareInputQueue)
                {
                    // avoid hammering the QueueDeclarePassive method
                    if (Interlocked.Increment(ref _queueDeclarePassiveCounter) % 100 == 0)
                    {
                        _consumer?.Model.QueueDeclarePassive(Address);
                    }
                }
            }
            catch
            {
            }

            if (_consumer == null)
            {
                // initialization must have failed
                return null;
            }

            if (!_consumer.Model.IsOpen)
            {
                _consumer.Dispose();
                _consumer = null;
                return null;
            }

            BasicDeliverEventArgs result;

            try
            {
                if (_blockOnReceive)
                {
                    result = await _consumer.Queue.Reader.ReadAsync(cancellationToken);
                }
                else
                {
                    // alternative way of receiving to fit in with Rebus' contract tests
                    using var timeout = new CancellationTokenSource(millisecondsDelay: 2000);
                    using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
                    var combinedCancellationToken = linkedTokenSource.Token;
                    try
                    {
                        result = await _consumer.Queue.Reader.ReadAsync(combinedCancellationToken);
                    }
                    catch (OperationCanceledException) when (combinedCancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }
            catch (ChannelClosedException)
            {
                _log.Warn("Closed channel detected - consumer will be disposed");
                _consumer?.Dispose();
                _consumer = null;
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log.Debug("Reading from queue was cancelled");
                return null;
            }

            if (result == null) return null;

            var deliveryTag = result.DeliveryTag;

            context.OnAck(async _ => _consumer.Model.BasicAck(deliveryTag, multiple: false));

            context.OnNack(async _ =>
            {
                // we might not be able to do this, but it doesn't matter that much if it succeeds
                try
                {
                    _consumer.Model.BasicNack(deliveryTag, multiple: false, requeue: true);
                }
                catch
                {
                }
            });

            return CreateTransportMessage(result.BasicProperties, result.Body.ToArray());
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            _log.Warn("Though it was possible to receive, but the channel turned out to be closed... it will be renewed after a short wait");
            _consumer?.Dispose();
            _consumer = null;
            await Task.Delay(30000, cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            await Task.Delay(1000, cancellationToken);

            throw new RebusApplicationException(exception,
                $"Unexpected exception thrown while trying to dequeue a message from rabbitmq, queue address: {Address}");
        }
    }

    void ReconnectQueue()
    {
        CreateQueue(Address);

        var subscriptionTasks = _registeredSubscriptions
            .Select(x => RegisterSubscriber(x.Topic, x.QueueName))
            .ToArray();

        Task.WaitAll(subscriptionTasks);
    }

    /// <summary>
    /// Creates the transport message.
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    /// <returns>the TransportMessage</returns>
    internal static TransportMessage CreateTransportMessage(IBasicProperties basicProperties, byte[] body)
    {
        string GetStringValue(KeyValuePair<string, object> kvp)
        {
            var headerValue = kvp.Value;

            if (headerValue is byte[] headerValueBytes)
            {
                return HeaderValueEncoding.GetString(headerValueBytes);
            }

            return headerValue?.ToString();
        }

        var headers = basicProperties.Headers?.ToDictionary(kvp => kvp.Key, GetStringValue)
                      ?? new Dictionary<string, string>();

        if (!headers.ContainsKey(Headers.MessageId))
        {
            AddMessageId(headers, basicProperties, body);
        }

        if (basicProperties.IsUserIdPresent())
        {
            headers[RabbitMqHeaders.UserId] = basicProperties.UserId;
        }

        if (basicProperties.IsCorrelationIdPresent())
        {
            headers[RabbitMqHeaders.CorrelationId] = basicProperties.CorrelationId;
        }

        if (basicProperties.Headers?.TryGetValue("x-delivery-count", out var deliveryCountObj) == true)
        {
            if (deliveryCountObj is byte[] bytes)
            {
                var deliveryCountString = Encoding.ASCII.GetString(bytes);
                var deliveryCount = int.TryParse(deliveryCountString, out var result) ? result : 0;

                headers[Headers.DeliveryCount] = deliveryCount.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var deliveryCount = Convert.ToInt32(deliveryCountObj);

                headers[Headers.DeliveryCount] = deliveryCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        return new TransportMessage(headers, body);
    }

    static void AddMessageId(IDictionary<string, string> headers, IBasicProperties basicProperties, byte[] body)
    {
        if (basicProperties.IsMessageIdPresent())
        {
            headers[Headers.MessageId] = basicProperties.MessageId;
            return;
        }

        var pseudoMessageId = GenerateMessageIdFromBodyContents(body);

        headers[Headers.MessageId] = pseudoMessageId;
    }

    static string GenerateMessageIdFromBodyContents(byte[] body)
    {
        if (body == null) return "MESSAGE-BODY-IS-NULL";

        var base64String = Convert.ToBase64String(body);

        return $"knuth-{Knuth.CalculateHash(base64String)}";
    }

    internal IModel CreateChannel()
    {
        var connection = _connectionManager.GetConnection();

        var channel = connection.CreateModel();
        _callbackOptions.ConfigureEvents(channel);
        return channel;
    }

    /// <summary>
    /// Creates the consumer.
    /// </summary>
    CustomQueueingConsumer InitializeConsumer()
    {
        IModel model = null;
        try
        {
            model = CreateChannel();
            model.BasicQos(prefetchSize: 0, prefetchCount: _maxMessagesToPrefetch, global: false);

            var consumer = new CustomQueueingConsumer(model);

            model.BasicConsume(queue: Address, autoAck: false, consumer: consumer,
                consumerTag: _consumerTag != null ? $"{_consumerTag}-{Path.GetRandomFileName().Replace(".", "")}" : ""
                );

            _log.Info("Successfully initialized consumer for {queueName}", Address);

            return consumer;
        }
        catch (OperationInterruptedException exception) when (exception.HasReplyCode(QueueDoesNotExist))
        {
            model.SafeDrop();
            _log.Warn("Queue not found - attempting to recreate queue and restore subscriptions.");
            ReconnectQueue();
            return null;
        }
        catch (Exception)
        {
            model.SafeDrop();
            throw;
        }
    }

    void DoSend(IReadOnlyList<OutgoingTransportMessage> outgoingMessages, IModel model, bool isExpress)
    {
        if (!outgoingMessages.Any()) return;

        if (_publisherConfirmsEnabled && !isExpress)
        {
            model.ConfirmSelect();
        }

        if (_batchSize == 1)
        {
            DoNormalSend(outgoingMessages, model);
        }
        else
        {
            DoBatchedSend(outgoingMessages, model, _batchSize);
        }

        if (_publisherConfirmsEnabled && !isExpress)
        {
            if (_publisherConfirmsTimeout == TimeSpan.Zero)
            {
                model.WaitForConfirmsOrDie();
            }
            else
            {
                model.WaitForConfirmsOrDie(_publisherConfirmsTimeout);
            }
        }
    }

    void DoBatchedSend(IReadOnlyList<OutgoingTransportMessage> outgoingMessages, IModel model, int batchSize)
    {
        foreach (var outgoingMessageBatch in outgoingMessages.Batch(batchSize))
        {
            var batch = model.CreateBasicPublishBatch();

            foreach (var outgoingMessage in outgoingMessageBatch)
            {
                var info = GetPublishInfo(model, outgoingMessage);

                batch.Add(
                    exchange: info.Exchange,
                    routingKey: info.RoutingKey,
                    mandatory: info.Mandatory,
                    properties: info.Properties,
                    body: info.Body
                );
            }

            batch.Publish();
        }
    }

    void DoNormalSend(IReadOnlyList<OutgoingTransportMessage> outgoingMessages, IModel model)
    {
        foreach (var outgoingMessage in outgoingMessages)
        {
            var info = GetPublishInfo(model, outgoingMessage);

            model.BasicPublish(
                exchange: info.Exchange,
                routingKey: info.RoutingKey,
                mandatory: info.Mandatory,
                basicProperties: info.Properties,
                body: info.Body
            );
        }
    }

    RabbitMqPublishInfo GetPublishInfo(IModel model, OutgoingTransportMessage outgoingMessage)
    {
        var destinationAddress = outgoingMessage.DestinationAddress;
        var message = outgoingMessage.TransportMessage;
        var props = CreateBasicProperties(model, message.Headers);

        var mandatory = message.Headers.ContainsKey(RabbitMqHeaders.Mandatory);
        if (mandatory && !_callbackOptions.HasMandatoryCallback)
        {
            throw new MandatoryDeliveryException(
                "Mandatory delivery is not allowed without registering a handler for BasicReturn in RabbitMqOptions.");
        }

        var fullyQualifiedRoutingKey = new FullyQualifiedRoutingKey(destinationAddress);
        var exchange = fullyQualifiedRoutingKey.ExchangeName ?? _directExchangeName;

        var isPointToPoint = message.Headers.TryGetValue(Headers.Intent, out var intent)
                             && intent == Headers.IntentOptions.PointToPoint;

        // when we're sending point-to-point, we want to be sure that we are never sending the message out into nowhere
        if (isPointToPoint && !_callbackOptions.HasMandatoryCallback)
        {
            EnsureQueueExists(fullyQualifiedRoutingKey, model);
        }

        var routingKey = fullyQualifiedRoutingKey.RoutingKey;

        return new RabbitMqPublishInfo(
            Exchange: exchange,
            RoutingKey: routingKey,
            Mandatory: mandatory,
            Properties: props,
            Body: new ReadOnlyMemory<byte>(message.Body)
        );
    }

    record struct RabbitMqPublishInfo(
        string Exchange,
        string RoutingKey,
        bool Mandatory,
        IBasicProperties Properties,
        ReadOnlyMemory<byte> Body);

    static readonly ConcurrentDictionary<string, (string, string)> ContentTypeHeadersToContentTypeAndCharsetMappings = new();

    static IBasicProperties CreateBasicProperties(IModel model, Dictionary<string, string> headers)
    {
        var props = model.CreateBasicProperties();

        if (headers.TryGetValue(RabbitMqHeaders.MessageId, out var messageId))
        {
            props.MessageId = messageId;
        }

        if (headers.TryGetValue(RabbitMqHeaders.AppId, out var appId))
        {
            props.AppId = appId;
        }

        if (headers.TryGetValue(RabbitMqHeaders.CorrelationId, out var correlationId))
        {
            props.CorrelationId = correlationId;
        }

        if (headers.TryGetValue(RabbitMqHeaders.UserId, out var userId))
        {
            props.UserId = userId;
        }

        if (headers.TryGetValue(RabbitMqHeaders.ContentType, out var contentTypeHeaderValue))
        {
            var (contentType, charset) = ContentTypeHeadersToContentTypeAndCharsetMappings.GetOrAdd(contentTypeHeaderValue, GetContentTypeAndEncodingValues);

            props.ContentType = contentType;
            props.ContentEncoding = charset;
        }

        if (headers.TryGetValue(RabbitMqHeaders.ContentEncoding, out var contentEncoding))
        {
            props.ContentEncoding = contentEncoding;
        }

        if (headers.TryGetValue(RabbitMqHeaders.DeliveryMode, out var deliveryModeVal))
        {
            if (byte.TryParse(deliveryModeVal, out var deliveryMode) && deliveryMode > 0 && deliveryMode <= 2)
            {
                props.DeliveryMode = deliveryMode;
            }
        }

        if (headers.TryGetValue(RabbitMqHeaders.Expiration, out var expiration))
        {
            if (TimeSpan.TryParse(expiration, out var timeToBeDelivered))
            {
                props.Expiration = timeToBeDelivered.TotalMilliseconds.ToString("0");
            }
        }

        if (headers.TryGetValue(RabbitMqHeaders.Priority, out var priorityVal))
        {
            if (byte.TryParse(priorityVal, out var priority))
            {
                props.Priority = priority;
            }
        }

        if (headers.TryGetValue(RabbitMqHeaders.Timestamp, out var timestampVal))
        {
            if (DateTimeOffset.TryParse(timestampVal, out var timestamp))
            {
                // Unix epoch
                var unixTime = (long)(timestamp.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
                props.Timestamp = new AmqpTimestamp(unixTime);
            }
        }

        if (headers.TryGetValue(RabbitMqHeaders.Type, out var type))
        {
            props.Type = type;
        }

        // Alternative way of setting RabbitMqHeaders.DeliveryMode
        if (!headers.ContainsKey(RabbitMqHeaders.DeliveryMode))
        {
            var express = headers.ContainsKey(Headers.Express);
            props.Persistent = !express;
        }

        // must be last because the other functions on the headers might change them
        props.Headers = headers
            .ToDictionary(kvp => kvp.Key,
                kvp =>
                {
                    if (kvp.Value != null)
                    {
                        const int maxHeaderLengthInBytes = 2 << 15;

                        var value = kvp.Value;
                        var bytes = HeaderValueEncoding.GetBytes(value);

                        // be sure that header values aren't too big
                        return bytes.Truncate(maxHeaderLengthInBytes);
                    }

                    return default(object);
                });

        return props;
    }

    static (string, string) GetContentTypeAndEncodingValues(string input)
    {
        var parts = input.Split(';');

        var contentTypeResult = parts[0];
        var charsetResult = default(string);

        // if the MIME type has parameters, then we see if there's a charset in there...
        if (parts.Length > 1)
        {
            try
            {
                var parameters = parts.Skip(1)
                    .Select(p => p.Split('='))
                    .ToDictionary(p => p.First(), p => p.LastOrDefault());

                if (parameters.TryGetValue("charset", out var result))
                {
                    charsetResult = result;
                }
            }
            catch
            {
            }
        }

        return (contentTypeResult, charsetResult);
    }

    void EnsureQueueExists(FullyQualifiedRoutingKey routingKey, IModel model)
    {
        _verifiedQueues
            .GetOrAdd(routingKey, _ => CheckQueueExistence(routingKey, model));
    }

    bool CheckQueueExistence(FullyQualifiedRoutingKey routingKey, IModel model)
    {
        var queueName = routingKey.RoutingKey;

        try
        {
            // Checks if queue exists, throws if the queue doesn't exist
            model.QueueDeclarePassive(queueName);
        }
        catch (Exception e)
        {
            throw new RebusApplicationException(e, $"Queue '{queueName}' does not exist");
        }

        var exchange = routingKey.ExchangeName ?? _directExchangeName;

        try
        {
            model.ExchangeDeclarePassive(exchange);
        }
        catch (Exception e)
        {
            throw new RebusApplicationException(e, $"Exchange '{exchange}' does not exist");
        }

        // since we can't check whether a binding exists, we need
        // to proactively declare it, so we don't risk sending a message
        // "into the void":
        model.QueueBind(queueName, exchange, routingKey.RoutingKey);

        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writerPool.Dispose();
        _consumer?.Dispose();
        _connectionManager.Dispose();
    }

    /// <summary>
    /// Gets "subscriber addresses" as one single magic "queue address", which will be interpreted
    /// as a proper pub/sub topic when the time comes to send to it
    /// </summary>
    public async Task<IReadOnlyList<string>> GetSubscriberAddresses(string topic)
    {
        return topic.Contains('@')
            ? new[] { topic }
            : new[] { $"{topic}@{_topicExchangeName}" };
    }

    /// <summary>
    /// Registers the queue address as a subscriber of the given topic by creating an appropriate binding
    /// </summary>
    public async Task RegisterSubscriber(string topic, string subscriberAddress)
    {
        var connection = _connectionManager.GetConnection();
        var subscription = ParseSubscription(topic, subscriberAddress);

        _log.Debug("Registering subscriber {subscription}", subscription);

        using (var model = connection.CreateModel())
        {
            model.QueueBind(subscription.QueueName, subscription.Exchange, subscription.Topic);
        }

        await _subscriptionSemaphore.WaitAsync();
        try
        {
            if (!_registeredSubscriptions.Contains(subscription))
            {
                _registeredSubscriptions.Add(subscription);
            }
        }
        finally
        {
            _subscriptionSemaphore.Release();
        }
    }

    /// <summary>
    /// Unregisters the queue address as a subscriber of the given topic by removing the appropriate binding
    /// </summary>
    public async Task UnregisterSubscriber(string topic, string subscriberAddress)
    {
        var connection = _connectionManager.GetConnection();
        var subscription = ParseSubscription(topic, subscriberAddress);

        _log.Debug("Unregistering subscriber {subscription}", subscription);

        using (var model = connection.CreateModel())
        {
            model.QueueUnbind(subscription.QueueName, subscription.Exchange, subscription.Topic,
                new Dictionary<string, object>());
        }

        await _subscriptionSemaphore.WaitAsync();
        try
        {
            if (_registeredSubscriptions.Contains(subscription))
            {
                _registeredSubscriptions.Remove(subscription);
            }
        }
        finally
        {
            _subscriptionSemaphore.Release();
        }
    }

    Subscription ParseSubscription(string topicPossiblyQualified, string queueName)
    {
        if (topicPossiblyQualified.Contains('@'))
        {
            var parts = topicPossiblyQualified.Split('@');

            if (parts.Length != 2)
            {
                throw new FormatException(
                    $"Could not parse the topic '{topicPossiblyQualified}' into an exchange-qualified topic - expected the format <topic>@<exchange>");
            }

            return new Subscription(parts[1], parts[0], queueName);
        }

        return new Subscription(_topicExchangeName, topicPossiblyQualified, queueName);
    }

    /// <summary>
    /// Gets whether this transport is centralized (it always is, as RabbitMQ's bindings are used to do proper pub/sub messaging)
    /// </summary>
    public bool IsCentralized => true;

    /// <summary>
    /// Represents a subscription of a queue address to a topic.
    /// </summary>
    record Subscription
    {
        /// <summary>
        /// Initializes a new <see cref="Subscription"/> instance.
        /// </summary>
        /// <param name="exchange">The exchange for the routing key</param>
        /// <param name="topic">The topic for the subscription.</param>
        /// <param name="queueName">The queue address of the subscriber.</param>
        public Subscription(string exchange, string topic, string queueName)
        {
            Exchange = exchange;
            Topic = topic;
            QueueName = queueName;
        }

        /// <summary>
        /// Gets the exchange
        /// </summary>
        public string Exchange { get; }

        /// <summary>
        /// Gets the topic for the subscription.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the subscriber address for the subscription.
        /// </summary>
        public string QueueName { get; }

        public override string ToString() => $"{Topic}@{Exchange} => {QueueName}";
    }
}
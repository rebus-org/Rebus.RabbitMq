﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable ArgumentsStyleLiteral

#pragma warning disable 1998

namespace Rebus.RabbitMq
{
    /// <summary>
    /// Implementation of <see cref="ITransport"/> that uses RabbitMQ to send/receive messages
    /// </summary>
    public class RabbitMqTransport : ITransport, IDisposable, IInitializable, ISubscriptionStorage
    {
        const string CurrentModelItemsKey = "rabbitmq-current-model";
        const string OutgoingMessagesItemsKey = "rabbitmq-outgoing-messages";
        const int TwoSeconds = 2000;
        
        /// <summary>
        /// <see cref="ShutdownEventArgs.ReplyCode"/> value that indicates that a queue does not exist
        /// </summary>
        const int QueueDoesNotExist = 404;

        static readonly Encoding HeaderValueEncoding = Encoding.UTF8;

        readonly ConcurrentQueue<CustomQueueingConsumer> _consumers = new ConcurrentQueue<CustomQueueingConsumer>();
        readonly ConcurrentQueue<IModel> _models = new ConcurrentQueue<IModel>();

        readonly ConcurrentDictionary<FullyQualifiedRoutingKey, bool> _verifiedQueues = new ConcurrentDictionary<FullyQualifiedRoutingKey, bool>();
        readonly List<Subscription> _registeredSubscriptions = new List<Subscription>();
        readonly SemaphoreSlim _subscriptionSemaphore = new SemaphoreSlim(1, 1);
        readonly ConnectionManager _connectionManager;
        readonly ILog _log;

        ushort _maxMessagesToPrefetch;

        bool _declareExchanges = true;
        bool _declareInputQueue = true;
        bool _bindInputQueue = true;
        bool _publisherConfirmsEnabled = true;

        string _directExchangeName = RabbitMqOptionsBuilder.DefaultDirectExchangeName;
        string _topicExchangeName = RabbitMqOptionsBuilder.DefaultTopicExchangeName;

        RabbitMqCallbackOptionsBuilder _callbackOptions = new RabbitMqCallbackOptionsBuilder();
        RabbitMqQueueOptionsBuilder _inputQueueOptions = new RabbitMqQueueOptionsBuilder();
        RabbitMqQueueOptionsBuilder _defaultQueueOptions = new RabbitMqQueueOptionsBuilder();
        RabbitMqExchangeOptionsBuilder _inputExchangeOptions = new RabbitMqExchangeOptionsBuilder();

        RabbitMqTransport(IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch, string inputQueueAddress)
        {
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
            if (maxMessagesToPrefetch <= 0) throw new ArgumentException($"Cannot set 'maxMessagesToPrefetch' to {maxMessagesToPrefetch} - it must be at least 1!");

            _maxMessagesToPrefetch = (ushort)maxMessagesToPrefetch;

            _log = rebusLoggerFactory.GetLogger<RabbitMqTransport>();

            Address = inputQueueAddress;
        }

        /// <summary>
        /// Constructs the RabbitMQ transport with multiple connection endpoints. They will be tried in random order until working one is found
        ///  Credentials will be extracted from the connectionString of the first provided endpoint
        /// </summary>
        public RabbitMqTransport(IList<ConnectionEndpoint> endpoints, string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch = 50, Func<IConnectionFactory, IConnectionFactory> customizer = null)
            : this(rebusLoggerFactory, maxMessagesToPrefetch, inputQueueAddress)
        {
            if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));

            _connectionManager = new ConnectionManager(endpoints, inputQueueAddress, rebusLoggerFactory, customizer);
        }

        /// <summary>
        /// Constructs the transport with a connection to the RabbitMQ instance specified by the given connection string.
        /// Multiple connection strings could be provided. They should be separates with , or ; 
        /// </summary>
        public RabbitMqTransport(string connectionString, string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory, int maxMessagesToPrefetch = 50, Func<IConnectionFactory, IConnectionFactory> customizer = null)
            : this(rebusLoggerFactory, maxMessagesToPrefetch, inputQueueAddress)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            _connectionManager = new ConnectionManager(connectionString, inputQueueAddress, rebusLoggerFactory, customizer);
        }

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
        /// Sets whether to use the publisher confirms protocol
        /// </summary>
        public void EnablePublisherConfirms(bool value = true)
        {
            _publisherConfirmsEnabled = value;
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
            _inputExchangeOptions = inputExchangeOptions ?? throw new ArgumentNullException(nameof(inputExchangeOptions));
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
        public void CreateQueue(string address)
        {
            var connection = _connectionManager.GetConnection();

            try
            {
                using (var model = connection.CreateModel())
                {
                    const bool durable = true;

                    if (_declareExchanges)
                    {
                        model.ExchangeDeclare(_directExchangeName, ExchangeType.Direct, durable, arguments: _inputExchangeOptions.DirectExchangeArguments);
                        model.ExchangeDeclare(_topicExchangeName, ExchangeType.Topic, durable, arguments: _inputExchangeOptions.TopicExchangeArguments);
                    }

                    if (_declareInputQueue)
                    {
                        DeclareQueue(address, model);
                    }

                    if (_bindInputQueue)
                    {
                        BindInputQueue(address, model);
                    }
                }
            }
            catch (Exception exception)
            {
                throw new RebusApplicationException(exception, $"Queue declaration for '{address}' failed");
            }
        }

        void BindInputQueue(string address, IModel model)
        {
            model.QueueBind(address, _directExchangeName, address);
        }

        void DeclareQueue(string address, IModel model)
        {
            if (Address != null && Address.Equals(address))
            {
                // This is the input queue => we use the queue setting to create the queue
                model.QueueDeclare(address,
                    exclusive: _inputQueueOptions.Exclusive,
                    durable: _inputQueueOptions.Durable,
                    autoDelete: _inputQueueOptions.AutoDelete,
                    arguments: _inputQueueOptions.Arguments);
            }
            else
            {
                // This is another queue, probably the error queue => we use the default queue options
                model.QueueDeclare(address,
                    exclusive: _defaultQueueOptions.Exclusive,
                    durable: _defaultQueueOptions.Durable,
                    autoDelete: _defaultQueueOptions.AutoDelete,
                    arguments: _defaultQueueOptions.Arguments);
            }
        }

        /// <inheritdoc />
        public async Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            var outgoingMessages = context.GetOrAdd(OutgoingMessagesItemsKey, () =>
            {
                var messages = new ConcurrentQueue<OutgoingMessage>();

                context.OnCommitted(tc => SendOutgoingMessages(context, messages));

                return messages;
            });

            outgoingMessages.Enqueue(new OutgoingMessage(destinationAddress, message, isExpress: message.Headers.ContainsKey(Headers.Express)));
        }

        /// <inheritdoc />
        public string Address { get; }

        /// <summary>
        /// Deletes all messages from the queue
        /// </summary>
        public void PurgeInputQueue()
        {
            var connection = _connectionManager.GetConnection();

            using (var model = connection.CreateModel())
            {
                try
                {
                    model.QueuePurge(Address);
                }
                catch (OperationInterruptedException exception) when (exception.HasReplyCode(QueueDoesNotExist))
                {
                    // ignore this error if the queue does not exist
                }
            }
        }

        /// <inheritdoc />
        public async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            if (Address == null)
            {
                throw new InvalidOperationException("This RabbitMQ transport does not have an input queue - therefore, it is not possible to receive anything");
            }

            try
            {
                if (!_consumers.TryDequeue(out var consumer))
                {
                    consumer = InitializeConsumer();
                }
                else
                {
                    try
                    {
                        // When a consumer is dequeued from the the "consumers" pool, it might be bound to a queue, which does not exist anymore,
                        // eg. expired and deleted by RabbitMQ server policy). In this case this calling QueueDeclarePassive will result in 
                        // an OperationInterruptedException and "consumer.Model.IsOpen" will be set to false (this is handled later in the code by 
                        // disposing this consumer). There is no need to handle this exception. The logic of InitializeConsumer() will make sure 
                        // that the queue is recreated later based on assumption about how ReBus is handling null-result of ITransport.Receive().
                        consumer?.Model.QueueDeclarePassive(Address);
                    }
                    catch { }
                }

                if (consumer == null)
                {
                    // initialization must have failed
                    return null;
                }

                if (!consumer.Model.IsOpen)
                {
                    consumer.Dispose();
                    return null;
                }

                context.OnDisposed((tc) => _consumers.Enqueue(consumer));

                var result = await consumer.GetNext(cancellationToken); 

                // This should rarely, if ever, be null now
                if (result == null) return null;

                // ensure we use the consumer's model throughtout the handling of this message
                context.Items[CurrentModelItemsKey] = consumer.Model;

                var deliveryTag = result.DeliveryTag;

                context.OnCompleted(async (tc) =>
                {
                    var model = GetModel(context);
                    model.BasicAck(deliveryTag, multiple: false);
                });

                context.OnAborted((tc) =>
                {
                    // we might not be able to do this, but it doesn't matter that much if it succeeds
                    try
                    {
                        var model = GetModel(context);
                        model.BasicNack(deliveryTag, multiple: false, requeue: true);
                    }
                    catch { }
                });

                return CreateTransportMessage(result.BasicProperties, result.Body);
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Thread.Sleep(1000);

                throw new RebusApplicationException(exception, $"Unexpected exception thrown while trying to dequeue a message from rabbitmq, queue address: {Address}");
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
        internal static TransportMessage CreateTransportMessage(IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            var bytes = body.ToArray();

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
                AddMessageId(headers, basicProperties, bytes);
            }

            if (basicProperties.IsUserIdPresent())
            {
                headers[RabbitMqHeaders.UserId] = basicProperties.UserId;
            }

            if (basicProperties.IsCorrelationIdPresent())
            {
                headers[RabbitMqHeaders.CorrelationId] = basicProperties.CorrelationId;
            }

            return new TransportMessage(headers, bytes);
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

        /// <summary>
        /// Creates the consumer.
        /// </summary>
        CustomQueueingConsumer InitializeConsumer()
        {
            IConnection connection = null;
            IModel model = null;
            try
            {
                // receive must be done with separate model
                connection = _connectionManager.GetConnection();

                model = connection.CreateModel();
                model.BasicQos(0, _maxMessagesToPrefetch, false);

                var consumer = new CustomQueueingConsumer(model, _maxMessagesToPrefetch);

                model.BasicConsume(Address, false, consumer);

                _log.Info("Successfully initialized consumer for {queueName}", Address);

                return consumer;
            }
            catch (OperationInterruptedException exception) when (exception.HasReplyCode(QueueDoesNotExist))
            {
                _log.Warn("Queue not found - attempting to recreate queue and restore subscriptions.");
                ReconnectQueue();
                return null;
            }
            catch (Exception)
            {
                try
                {
                    model?.Dispose();
                }
                catch { }

                try
                {
                    connection?.Dispose();
                }
                catch { }

                throw;
            }
        }

        async Task SendOutgoingMessages(ITransactionContext context, ConcurrentQueue<OutgoingMessage> outgoingMessages)
        {
            var model = GetModel(context);

            var expressGroups = outgoingMessages
                .GroupBy(o => o.IsExpress)
                .OrderByDescending(g => g.Key); //< send express messages first

            foreach (var expressGroup in expressGroups)
            {
                DoSend(outgoingMessages, model, isExpress: expressGroup.Key);
            }
        }

        void DoSend(ConcurrentQueue<OutgoingMessage> outgoingMessages, IModel model, bool isExpress)
        {
            if (_publisherConfirmsEnabled && !isExpress)
            {
                model.ConfirmSelect();
            }

            foreach (var outgoingMessage in outgoingMessages)
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

                var routingKey = new FullyQualifiedRoutingKey(destinationAddress);
                var exchange = routingKey.ExchangeName ?? _directExchangeName;

                var isPointToPoint = message.Headers.TryGetValue(Headers.Intent, out var intent)
                                     && intent == Headers.IntentOptions.PointToPoint;

                // when we're sending point-to-point, we want to be sure that we are never sending the message out into nowhere
                if (isPointToPoint && !_callbackOptions.HasMandatoryCallback)
                {
                    EnsureQueueExists(routingKey, model);
                }

                model.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey.RoutingKey,
                    mandatory: mandatory,
                    basicProperties: props,
                    body: message.Body
                );
            }

            if (_publisherConfirmsEnabled && !isExpress)
            {
                model.WaitForConfirmsOrDie();
            }
        }

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

            if (headers.TryGetValue(RabbitMqHeaders.ContentType, out var contentType))
            {
                var parts = contentType.Split(';');

                props.ContentType = parts[0];

                // if the MIME type has parameters, then we see if there's a charset in there...
                if (parts.Length > 1)
                {
                    try
                    {
                        var parameters = parts.Skip(1)
                            .Select(p => p.Split('='))
                            .ToDictionary(p => p.First(), p => p.LastOrDefault());

                        if (parameters.TryGetValue("charset", out var charset))
                        {
                            props.ContentEncoding = charset;
                        }
                    }
                    catch { }
                }
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
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value != null ? (object)HeaderValueEncoding.GetBytes(kvp.Value) : null);

            return props;
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

        IModel GetModel(ITransactionContext context)
        {
            IModel GetOpenModelFromPool()
            {
                while (_models.TryDequeue(out var modelFromPool))
                {
                    // did we find a usable model?
                    if (modelFromPool.IsOpen)
                    {
                        // when yes: put it back in the pool when we're done and return it
                        context.OnDisposed(tc => _models.Enqueue(modelFromPool));
                        return modelFromPool;
                    }

                    // if the model is not open, we close&dispose it
                    try
                    {
                        _log.Debug("Found out current model was closed... disposing it");
                        modelFromPool.Close();
                        modelFromPool.Dispose();
                    }
                    catch
                    {
                    }
                }

                _log.Debug("Initializing new model");
                var connection = _connectionManager.GetConnection();
                var newModel = connection.CreateModel();

                context.OnDisposed(tc => _models.Enqueue(newModel));

                // Configure registered events on model
                _callbackOptions?.ConfigureEvents(newModel);

                return newModel;
            }

            var model = context.GetOrAdd(CurrentModelItemsKey, GetOpenModelFromPool);

            return model;
        }

        class OutgoingMessage
        {
            public OutgoingMessage(string destinationAddress, TransportMessage transportMessage, bool isExpress)
            {
                DestinationAddress = destinationAddress;
                TransportMessage = transportMessage;
                IsExpress = isExpress;
            }

            public string DestinationAddress { get; }
            public TransportMessage TransportMessage { get; }
            public bool IsExpress { get; }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            while (_models.TryDequeue(out var model))
            {
                try
                {
                    model.Close();
                    model.Dispose();
                }
                catch { }
            }

            while (_consumers.TryDequeue(out var consumer))
            {
                try
                {
                    consumer.Dispose();
                }
                catch { }
            }

            _connectionManager.Dispose();
        }

        /// <summary>
        /// Gets "subscriber addresses" as one single magic "queue address", which will be interpreted
        /// as a proper pub/sub topic when the time comes to send to it
        /// </summary>
        public async Task<string[]> GetSubscriberAddresses(string topic)
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
                model.QueueUnbind(subscription.QueueName, subscription.Exchange, subscription.Topic, new Dictionary<string, object>());
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
                    throw new FormatException($"Could not parse the topic '{topicPossiblyQualified}' into an exchange-qualified topic - expected the format <topic>@<exchange>");
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
        class Subscription
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

            protected bool Equals(Subscription other)
            {
                return Exchange == other.Exchange && Topic == other.Topic && QueueName == other.QueueName;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Subscription)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Exchange != null ? Exchange.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Topic != null ? Topic.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (QueueName != null ? QueueName.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public static bool operator ==(Subscription left, Subscription right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Subscription left, Subscription right)
            {
                return !Equals(left, right);
            }
        }
    }
}

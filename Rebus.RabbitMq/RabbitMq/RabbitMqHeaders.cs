using Rebus.Messages;

namespace Rebus.RabbitMq;

/// <summary>
/// Contains keys for RabbitMQ headers
/// </summary>
public static class RabbitMqHeaders
{
    /// <summary>
    /// The mandatory flag can be thought of as turning on fault detection mode, it will only cause RabbitMQ to notify you of failures, not success. Should the message route correctly, your publisher will not be notified.
    /// </summary>
    public const string Mandatory = "rabbitmq-mandatory";

    /// <summary>
    /// A unique identifier such as a UUID that your application can use to identify the message
    /// </summary>
    public const string MessageId = Headers.MessageId;

    /// <summary>
    /// Id of the application publishing messages
    /// </summary>
    public const string AppId = "rabbitmq-app-id";

    /// <summary>
    /// If the message is in reference to some other message or uniquely identifiable item, the correlation-id is a good way to indicate what the message is referencing
    /// </summary>
    public const string CorrelationId = "rabbitmq-corr-id";

    /// <summary>
    /// A free-form string that if used, RabbitMQ will validate against the connected user and drop messages if they do not match.
    /// </summary>
    public const string UserId = "rabbitmq-user-id";

    /// <summary>
    /// Specify type of the message body using mime-types
    /// </summary>
    public const string ContentType = Headers.ContentType;

    /// <summary>
    /// used for specifying content encoding, if your message body is encoded in some special way such as zlib, deflate or base64
    /// </summary>
    public const string ContentEncoding = Headers.ContentEncoding;

    /// <summary>
    /// Non-persistent (1) or persistent (2).
    /// </summary>
    public const string DeliveryMode = "rabbitmq-delivery-mode";

    /// <summary>
    /// An epoch or unix-timestamp value as a text string that indicates when the message should expire
    /// </summary>
    public const string Expiration = Headers.TimeToBeReceived;

    /// <summary>
    /// Property specified for the use for priority ordering in queues
    /// </summary>
    public const string Priority = "rabbitmq-priority";

    /// <summary>
    /// An epoch or unix-timestamp value that can be used to indicate when the message was created
    /// </summary>
    public const string Timestamp = "rabbitmq-timestamp";

    /// <summary>
    /// A text string your application can use to describe the message type or payload
    /// </summary>
    public const string Type = "rabbitmq-type";
}
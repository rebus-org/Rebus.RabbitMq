using System;
using System.Collections.Generic;
using Rebus.Messages;

namespace Rebus.RabbitMq;

/// <summary>
/// Contains all the information about a message returned from an AMQP broker within the Basic content-class.
/// </summary>
public class BasicReturnEventArgs : EventArgs
{
    /// <summary>
    /// Creates the event args with the given values
    /// </summary>
    public BasicReturnEventArgs(TransportMessage message, string exchange, int replyCode, string replyText, string routingKey)
    {
        Message = message;
        Exchange = exchange;
        ReplyCode = replyCode;
        ReplyText = replyText;
        RoutingKey = routingKey;
    }

    /// <summary>The transport message.</summary>
    public TransportMessage Message { get; }

    /// <summary>
    /// Gets the headers of the message
    /// </summary>
    public Dictionary<string, string> Headers => Message.Headers;

    /// <summary>The exchange the returned message was originally
    /// published to.</summary>
    public string Exchange { get; }

    /// <summary>The AMQP reason code for the return. See
    /// RabbitMQ.Client.Framing.*.Constants.</summary>
    public int ReplyCode { get; }

    /// <summary>Human-readable text from the broker describing the
    /// reason for the return.</summary>
    public string ReplyText { get; }

    /// <summary>The routing key used when the message was
    /// originally published.</summary>
    public string RoutingKey { get; }
}
using System;
using RabbitMQ.Client;
using Rebus.Messages;

namespace Rebus.RabbitMq
{
    /// <summary>
    /// Contains all the information about a message returned from an AMQP broker within the Basic content-class.
    /// </summary>
    public class BasicReturnEventArgs : EventArgs
    {
        /// <summary>The transport message.</summary>
        public TransportMessage Message { get; set; }

        /// <summary>The message body.</summary>
        public byte[] Body { get; set; }

        /// <summary>The exchange the returned message was originally
        /// published to.</summary>
        public string Exchange { get; set; }

        /// <summary>The AMQP reason code for the return. See
        /// RabbitMQ.Client.Framing.*.Constants.</summary>
        public int ReplyCode { get; set; }

        /// <summary>Human-readable text from the broker describing the
        /// reason for the return.</summary>
        public string ReplyText { get; set; }

        /// <summary>The routing key used when the message was
        /// originally published.</summary>
        public string RoutingKey { get; set; }
    }
}

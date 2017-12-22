using System.Collections.Generic;

namespace Rebus.Config
{
    /// <summary>
    /// Allows for fluently configuring RabbitMQ input queue options
    /// </summary>
    public class RabbitMqQueueOptionsBuilder
    {
        /// <summary>
        /// Set the durability of the input queue
        /// </summary>
        public RabbitMqQueueOptionsBuilder SetDurable(bool durable)
        {
            Durable = durable;
            return this;
        }

        /// <summary>
        /// Set exclusiveness of the input queue
        /// </summary>
        public RabbitMqQueueOptionsBuilder SetExclusive(bool exclusive)
        {
            Exclusive = exclusive;
            return this;
        }

        /// <summary>
        /// Set auto delete
        /// </summary>
        public RabbitMqQueueOptionsBuilder SetAutoDelete(bool autoDelete)
        {
            AutoDelete = autoDelete;
            return this;
        }

        /// <summary>
        /// Set the arguments of the input queue
        /// </summary>
        public RabbitMqQueueOptionsBuilder SetArguments(Dictionary<string, object> arguments)
        {
            Arguments = arguments;
            return this;
        }

        /// <summary>
        /// Add input queue arguments to the default settings
        /// </summary>
        public RabbitMqQueueOptionsBuilder AddArgument(string key, string val)
        {
            Arguments.Add(key, val);
            return this;
        }

        internal bool Durable { get; private set; } = true;

        internal bool Exclusive { get; private set; } = false;

        internal bool AutoDelete { get; private set; } = false;

        internal Dictionary<string, object> Arguments { get; private set; } = new Dictionary<string, object>
        {
            {"x-ha-policy", "all"}
        };
    }
}

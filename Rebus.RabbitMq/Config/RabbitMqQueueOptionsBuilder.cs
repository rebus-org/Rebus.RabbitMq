using System.Collections.Generic;

namespace Rebus.Config
{
    public class RabbitMqQueueOptionsBuilder
    {
        public RabbitMqQueueOptionsBuilder SetDurable(bool durable)
        {
            Durable = durable;
            return this;
        }

        public RabbitMqQueueOptionsBuilder SetExclusive(bool exclusive)
        {
            Exclusive = exclusive;
            return this;
        }

        public RabbitMqQueueOptionsBuilder SetAutoDelete(bool autoDelete)
        {
            AutoDelete = autoDelete;
            return this;
        }

        public RabbitMqQueueOptionsBuilder SetArguments(Dictionary<string, object> arguments)
        {
            Arguments = arguments;
            return this;
        }

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

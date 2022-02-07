using System;
using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Rebus.Config;

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
    /// Set auto-delete propery when declaring the queue
    /// <param name="autoDelete">Whether queue should be deleted when the last consumer unsubscribes</param>
    /// </summary>
    public RabbitMqQueueOptionsBuilder SetAutoDelete(bool autoDelete)
    {
        AutoDelete = autoDelete;
        return this;
    }

    /// <summary>
    /// Configure for how long a queue can be unused before it is automatically deleted by setting x-expires argument
    /// </summary>
    /// <param name="ttlInMs">expiration period in milliseconds, </param>
    /// <exception cref="ArgumentException">if the argumnet value is 0 or less</exception>
    public RabbitMqQueueOptionsBuilder SetQueueTTL(long ttlInMs)
    {
        if (ttlInMs <= 0)
            throw new ArgumentException("Time must be in milliseconds and greater than 0", nameof(ttlInMs));

        Arguments.Add("x-expires", ttlInMs);

        return this;
    }


    /// <summary>
    /// Set auto delete, when last consumer disconnects and/or how long queue can stay unused until it is deleted as expired.
    /// Zero or negative values of ttlInMs are ignored (no queue expiration).
    /// <param name="autoDelete">Whether queue should be deleted</param>
    /// <param name="ttlInMs">Time to live (in milliseconds) after last subscriber disconnects</param>
    /// </summary>
    public RabbitMqQueueOptionsBuilder SetAutoDelete(bool autoDelete, long ttlInMs = 0)
    {
        SetAutoDelete(autoDelete);

        if (ttlInMs > 0)
            SetQueueTTL(ttlInMs);

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
    public RabbitMqQueueOptionsBuilder AddArgument(string key, object val)
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
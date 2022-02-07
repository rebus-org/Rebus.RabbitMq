using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Rebus.Config;

/// <summary>
/// Allows for fluently configuring RabbitMQ exchange options
/// </summary>
public class RabbitMqExchangeOptionsBuilder
{
    /// <summary>
    /// Add exchange arguments to the default settings for the direct exchange
    /// </summary>
    public RabbitMqExchangeOptionsBuilder AddArgumentToDirectExchange(string key, string val)
    {
        DirectExchangeArguments.Add(key, val);
        return this;
    }

    /// <summary>
    /// Add exchange arguments to the default settings for the topic exchange
    /// </summary>
    public RabbitMqExchangeOptionsBuilder AddArgumentToTopicExchange(string key, string val)
    {
        TopicExchangeArguments.Add(key, val);
        return this;
    }

    internal Dictionary<string, object> DirectExchangeArguments { get; } = new();

    internal Dictionary<string, object> TopicExchangeArguments { get; } = new();
}
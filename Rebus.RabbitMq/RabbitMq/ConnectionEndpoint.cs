using System;

namespace Rebus.RabbitMq;

/// <summary>
/// Represents settings that will be mapped to AmqpTcpEndpoint 
/// </summary>
public class ConnectionEndpoint
{
    /// <summary>
    /// Will be mapped to RabbitMq URI
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Wraps <see cref="ConnectionString"/> in a <see cref="Uri"/>
    /// </summary>
    public Uri ConnectionUri => new(ConnectionString);

    /// <summary>
    /// Will be mapped to RabbitMq SslOptions. Could be null
    /// </summary>
    public SslSettings SslSettings { get; set; }
}
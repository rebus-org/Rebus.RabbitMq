namespace Rebus.RabbitMq
{
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
        /// Will be mapped to RabbitMq SslOptions. Could be null
        /// </summary>
        public SslSettings SslSettings { get; set; }
    }
}
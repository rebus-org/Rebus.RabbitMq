using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RabbitMQ.Client;
using Rebus.Logging;

namespace Rebus.RabbitMq
{
    internal class ConnectionManager : IDisposable
    {
        private readonly object _activeConnectionLock = new object();
        private readonly ConnectionFactory _connectionFactory;
        private readonly IList<AmqpTcpEndpoint> _amqpTcpEndpoints;
        private readonly ILog _log;

        private IConnection _activeConnection;
        private bool _disposed;

        public ConnectionManager(string connectionString, string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

            _log = rebusLoggerFactory.GetLogger<ConnectionManager>();

            if (inputQueueAddress != null)
            {
                _log.Info("Initializing RabbitMQ connection manager for transport with input queue '{0}'", inputQueueAddress);
            }
            else
            {
                _log.Info("Initializing RabbitMQ connection manager for one-way transport");
            }

            _connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
                ClientProperties = CreateClientProperties(inputQueueAddress)
            };

            var uriStrings = connectionString.Split(";,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (uriStrings.Length == 0)
                throw new ArgumentException("Please remember to specify at least one connection string for a RabbitMQ server somewhere. You can also add multiple connection strings separated by ; or , which Rebus will use in failover scenarios");

            if (uriStrings.Length > 1)
                _log.Info("RabbitMQ transport has {0} connection strings available", uriStrings.Length);

            _amqpTcpEndpoints = uriStrings.Select(uriString => new AmqpTcpEndpoint(new Uri(uriString))).ToList();
        }

        public IConnection GetConnection()
        {
            var connection = _activeConnection;

            if (connection != null)
            {
                if (connection.IsOpen)
                {
                    return connection;
                }
            }

            lock (_activeConnectionLock)
            {
                connection = _activeConnection;

                if (connection != null)
                {
                    if (connection.IsOpen)
                    {
                        return connection;
                    }

                    _log.Info("Existing connection found to be CLOSED");

                    try
                    {
                        connection.Dispose();
                    }
                    catch { }
                }

                try
                {
                    _activeConnection = _connectionFactory.CreateConnection(_amqpTcpEndpoints);

                    return _activeConnection;
                }
                catch (Exception exception)
                {
                    _log.Warn("Could not establish connection: {0}", exception.Message);
                    Thread.Sleep(500); //Is there a reason for this?
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                lock (_activeConnectionLock)
                {
                    var connection = _activeConnection;

                    if (connection != null)
                    {
                        _log.Info("Disposing RabbitMQ connection");

                        // WTF?!?!? RabbitMQ client disposal can THROW!
                        try
                        {
                            connection.Dispose();
                            _activeConnection = null;
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        public void AddClientProperties(Dictionary<string, string> additionalClientProperties)
        {
            foreach (var kvp in additionalClientProperties)
            {
                _connectionFactory.ClientProperties[kvp.Key] = kvp.Value;
            }
        }

        private static IDictionary<string, object> CreateClientProperties(string inputQueueAddress)
        {
            var properties = new Dictionary<string, object>
            {
                {"Type", "Rebus/.NET"},
                {"Machine", Environment.MachineName},
                {"InputQueue", inputQueueAddress ?? "<one-way client>"},
            };

            var userDomainName = Environment.GetEnvironmentVariable("USERDOMAIN");

            if (userDomainName != null)
            {
                properties["Domain"] = userDomainName;
            }

            var username = Environment.GetEnvironmentVariable("USERNAME");

            if (username != null)
            {
                properties["User"] = userDomainName;
            }

            var currentProcess = Process.GetCurrentProcess();

            properties.Add("ProcessName", currentProcess.ProcessName);
            properties.Add("FileName", currentProcess.MainModule.FileName);

            return properties;
        }
    }
}
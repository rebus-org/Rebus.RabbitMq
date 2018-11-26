using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RabbitMQ.Client;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.RabbitMq;

namespace Rebus.Internals
{
    class ConnectionManager : IDisposable
    {
        readonly object _activeConnectionLock = new object();
        readonly IConnectionFactory _connectionFactory;
        readonly IList<AmqpTcpEndpoint> _amqpTcpEndpoints;
        readonly ILog _log;

        IConnection _activeConnection;
        bool _disposed;

        public ConnectionManager(IList<ConnectionEndpoint> endpoints, string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory, Func<IConnectionFactory, IConnectionFactory> customizer)
        {
            if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

            _log = rebusLoggerFactory.GetLogger<ConnectionManager>();

            if (inputQueueAddress != null)
            {
                _log.Info("Initializing RabbitMQ connection manager for transport with input queue {queueName}", inputQueueAddress);
            }
            else
            {
                _log.Info("Initializing RabbitMQ connection manager for one-way transport");
            }

            if (endpoints.Count == 0)
            {
                throw new ArgumentException("Please remember to specify at least one endpoints for a RabbitMQ server. You can also add multiple connection strings separated by ; or , which RabbitMq will use in failover scenarios");
            }

            if (endpoints.Count > 1)
            {
                _log.Info("RabbitMQ transport has {count} endpoints available", endpoints.Count);
            }

            endpoints.ForEach(endpoint =>
            {
                if (endpoint == null)
                {
                    throw new ArgumentException("Provided endpoint collection should not contain null values");
                }

                if (string.IsNullOrEmpty(endpoint.ConnectionString))
                {
                    throw new ArgumentException("null or empty value is not valid for ConnectionString");
                }
            });

            _connectionFactory = new ConnectionFactory
            {
                Uri = endpoints.First().ConnectionUri, //Use the first URI in the list for ConnectionFactory to pick the AMQP credentials, VirtualHost (if any)
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
                ClientProperties = CreateClientProperties(inputQueueAddress)
            };

            if (customizer != null)
            {
                _connectionFactory = customizer(_connectionFactory);
            }

            _amqpTcpEndpoints = endpoints
                .Select(endpoint =>
                {
                    try
                    {
                        return new AmqpTcpEndpoint(new Uri(endpoint.ConnectionString), ToSslOption(endpoint.SslSettings));
                    }
                    catch (Exception exception)
                    {
                        throw new FormatException($"Could not turn the connection string '{endpoint.ConnectionString}' into an AMQP TCP endpoint", exception);
                    }
                })
                .ToList();

        }
        
        public ConnectionManager(string connectionString, string inputQueueAddress, IRebusLoggerFactory rebusLoggerFactory, Func<IConnectionFactory, IConnectionFactory> customizer)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

            _log = rebusLoggerFactory.GetLogger<ConnectionManager>();

            if (inputQueueAddress != null)
            {
                _log.Info("Initializing RabbitMQ connection manager for transport with input queue {queueName}", inputQueueAddress);
            }
            else
            {
                _log.Info("Initializing RabbitMQ connection manager for one-way transport");
            }

            var uriStrings = connectionString.Split(";,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (uriStrings.Length == 0)
            {
                throw new ArgumentException("Please remember to specify at least one connection string for a RabbitMQ server somewhere. You can also add multiple connection strings separated by ; or , which Rebus will use in failover scenarios");
            }

            if (uriStrings.Length > 1)
            {
                _log.Info("RabbitMQ transport has {count} connection strings available", uriStrings.Length);
            }

            _connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(uriStrings.First()), //Use the first URI in the list for ConnectionFactory to pick the AMQP credentials (if any)
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
                ClientProperties = CreateClientProperties(inputQueueAddress)
            };

            if (customizer != null)
            {
                _connectionFactory = customizer(_connectionFactory);
            }

            _amqpTcpEndpoints = uriStrings
                .Select(uriString =>
                {
                    try
                    {
                        return new AmqpTcpEndpoint(new Uri(uriString));
                    }
                    catch (Exception exception)
                    {
                        throw new FormatException($"Could not turn the connection string '{uriString}' into an AMQP TCP endpoint", exception);
                    }
                })
                .ToList();
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
                        connection.Close();
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
                    _log.Warn("Could not establish connection: {message}", exception.Message);
                    Thread.Sleep(1000); // if CreateConnection fails fast for some reason, we wait a little while here to avoid thrashing tightly
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
                            connection.Close();
                            connection.Dispose();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            _activeConnection = null;
                        }
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

        public void SetSslOptions(SslSettings ssl)
        {
            _amqpTcpEndpoints.ForEach(endpoint => { endpoint.Ssl = ToSslOption(ssl); });
        }

        static SslOption ToSslOption(SslSettings ssl)
        {
            if (ssl == null)
                return new SslOption();

            var sslOption = new SslOption(ssl.ServerName, ssl.CertPath, ssl.Enabled)
            {
                CertPassphrase = ssl.CertPassphrase,
                Version = ssl.Version,
                AcceptablePolicyErrors = ssl.AcceptablePolicyErrors
            };

            return sslOption;
        }

        private IDictionary<string, object> CreateClientProperties(string inputQueueAddress)
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

            //TODO: Ideally this should be ported to retrieve AppDomain.CurrentDomain info for netframework and System.Reflection.Assembly information for netstandard            
            var processName = "n/a";
            var fileName = "n/a";
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                processName = currentProcess.ProcessName;
                fileName = currentProcess.MainModule.FileName;
            }
            catch { } //In case of potentially insufficient permissions to get process information

            properties.Add("ProcessName", processName);
            properties.Add("FileName", fileName);

            return properties;
        }
    }
}

using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Transport;

namespace Rebus.RabbitMq.Tests;

public class RabbitMqTransportFactory : ITransportFactory
{
    // connection string for default docker instance
    public const string ConnectionString = "amqp://guest:guest@localhost:5672";
        
    readonly List<IDisposable> _disposables = new();
    readonly HashSet<string> _queuesToDelete = new();

    public ITransport CreateOneWayClient()
    {
        return Create(null);
    }

    public ITransport Create(string inputQueueAddress)
    {
        var transport = CreateRabbitMqTransport(inputQueueAddress);

        _disposables.Add(transport);

        if (inputQueueAddress != null)
        {
            transport.PurgeInputQueue();
        }

        transport.Initialize();

        if (inputQueueAddress != null)
        {
            _queuesToDelete.Add(inputQueueAddress);
        }

        return transport;
    }

    public void CleanUp()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();

        foreach (var queue in _queuesToDelete)
        {
            DeleteQueue(queue);
        }
        _queuesToDelete.Clear();
    }

    public static void CreateQueue(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        model.QueueDeclare(queueName, true, false, false);
    }

    public static void DeleteQueue(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        model.QueueDelete(queueName);
    }

    public static bool QueueExists(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        try
        {
            model.QueueDeclarePassive(queueName);
            return true;
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
        {
            return false;
        }
    }

    public static void DeleteExchange(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        model.ExchangeDelete(exchangeName);
    }

    /// <summary>
    /// We check for the existence of the exchange with the name <paramref name="exchangeName"/> by creating another
    /// randomly named exchange and trying to bind from the randomly named one to the one we want to check the existence of.
    /// This causes an exception if the exchange with the name <paramref name="exchangeName"/> does not exists.
    /// </summary>
    public static bool ExchangeExists(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        try
        {
            const string nonExistentTopic = "6BE38CB8-089A-4B65-BA86-0801BBC064E9------DELETE-ME";
            const string fakeExchange = "FEBC2512-CEC6-46EB-A058-37F1A9642B35------DELETE-ME";

            model.ExchangeDeclare(fakeExchange, ExchangeType.Direct);

            try
            {
                model.ExchangeBind(exchangeName, fakeExchange, nonExistentTopic);
                model.ExchangeUnbind(exchangeName, fakeExchange, nonExistentTopic);

                return true;
            }
            finally
            {
                model.ExchangeDelete(fakeExchange);
            }
        }
        catch
        {
            return false;
        }
    }

    protected virtual RabbitMqTransport CreateRabbitMqTransport(string inputQueueAddress)
    {
        var rabbitMqTransport = new RabbitMqTransport(ConnectionString, inputQueueAddress, new ConsoleLoggerFactory(false));
        rabbitMqTransport.SetBlockOnReceive(blockOnReceive: false);
        return rabbitMqTransport;
    }
}
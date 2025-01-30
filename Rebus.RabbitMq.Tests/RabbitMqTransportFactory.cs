using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Transport;

namespace Rebus.RabbitMq.Tests;

public class RabbitMqTransportFactory : ITransportFactory
{
    // connection string for default docker instance
    public static string ConnectionString => RabbitMqTestContainerManager.GetConnectionString();// "amqp://guest:guest@localhost:5672";

    readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();
    readonly HashSet<string> _queuesToDelete = new();

    public ITransport CreateOneWayClient()
    {
        return Create(null);
    }

    public ITransport Create(string inputQueueAddress)
    {
        var transport = CreateRabbitMqTransport(inputQueueAddress);

        _asyncDisposables.Add(transport);

        if (inputQueueAddress != null)
        {
            transport.PurgeInputQueue().GetAwaiter().GetResult();
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
        
        foreach (var asyncDisposable in _asyncDisposables)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        foreach (var queue in _queuesToDelete)
        {
            DeleteQueue(queue).GetAwaiter().GetResult();
        }
        _queuesToDelete.Clear();
    }

    public static async Task CreateQueue(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        await model.QueueDeclareAsync(queueName, true, false, false);
    }

    public static async Task DeleteQueue(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        await model.QueueDeleteAsync(queueName);
    }

    public static async Task<bool> QueueExists(string queueName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        try
        {
            await model.QueueDeclarePassiveAsync(queueName);
            return true;
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
        {
            return false;
        }
    }

    public static async Task DeleteExchange(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        await model.ExchangeDeleteAsync(exchangeName);
    }

    /// <summary>
    /// We check for the existence of the exchange with the name <paramref name="exchangeName"/> by creating another
    /// randomly named exchange and trying to bind from the randomly named one to the one we want to check the existence of.
    /// This causes an exception if the exchange with the name <paramref name="exchangeName"/> does not exists.
    /// </summary>
    public static async Task<bool> ExchangeExists(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        try
        {
            const string nonExistentTopic = "6BE38CB8-089A-4B65-BA86-0801BBC064E9------DELETE-ME";
            const string fakeExchange = "FEBC2512-CEC6-46EB-A058-37F1A9642B35------DELETE-ME";

            await model.ExchangeDeclareAsync(fakeExchange, ExchangeType.Direct);

            try
            {
                await model.ExchangeBindAsync(exchangeName, fakeExchange, nonExistentTopic);
                await model.ExchangeUnbindAsync(exchangeName, fakeExchange, nonExistentTopic);

                return true;
            }
            finally
            {
                await model.ExchangeDeleteAsync(fakeExchange);
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
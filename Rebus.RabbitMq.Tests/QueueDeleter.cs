using System;
// ReSharper disable EmptyGeneralCatchClause

namespace Rebus.RabbitMq.Tests;

class QueueDeleter : IDisposable
{
    readonly string _queueName;

    public QueueDeleter(string queueName)
    {
        _queueName = queueName;
    }

    public void Dispose()
    {
        try
        {
            RabbitMqTransportFactory.DeleteQueue(_queueName).GetAwaiter().GetResult();

            Console.WriteLine($"Queue '{_queueName}' deleted");
        }
        catch { }
    }
}
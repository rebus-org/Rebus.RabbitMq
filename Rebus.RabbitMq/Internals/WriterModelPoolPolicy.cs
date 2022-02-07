using RabbitMQ.Client;
using Rebus.RabbitMq;

namespace Rebus.Internals;

internal class WriterModelPoolPolicy
{
    readonly RabbitMqTransport _transport;
            
    public WriterModelPoolPolicy(RabbitMqTransport transport)
    {
        _transport = transport;
    }
            
    public IModel Create()
    {
        return _transport.CreateChannel();
    }
}
namespace Rebus.RabbitMq.Tests;

public class RabbitMqTransportFactoryWithPublisherConfirms : RabbitMqTransportFactory
{
    protected override RabbitMqTransport CreateRabbitMqTransport(string inputQueueAddress)
    {
        var transport = base.CreateRabbitMqTransport(inputQueueAddress);
        transport.EnablePublisherConfirms();
        return transport;
    }
}
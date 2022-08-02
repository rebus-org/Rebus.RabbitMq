using System;

namespace Rebus.RabbitMq.Tests;

public class RabbitMqTransportFactoryWithPublisherConfirms : RabbitMqTransportFactory
{
    protected override RabbitMqTransport CreateRabbitMqTransport(string inputQueueAddress)
    {
        var transport = base.CreateRabbitMqTransport(inputQueueAddress);
        transport.EnablePublisherConfirms(value: true, timeout: TimeSpan.FromSeconds(60));
        return transport;
    }
}
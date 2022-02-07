using NUnit.Framework;
using Rebus.Tests.Contracts.Transports;
// ReSharper disable ArgumentsStyleLiteral

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqTransportPublisherConfirmsSendReceive : BasicSendReceive<RabbitMqTransportFactoryWithPublisherConfirms>
{
    protected override TransportBehavior Behavior => new(ReturnsNullWhenQueueIsEmpty: true);
}
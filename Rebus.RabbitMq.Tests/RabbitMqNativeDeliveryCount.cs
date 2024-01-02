using NUnit.Framework;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
[Explicit("Does not quite work yet")]
public class RabbitMqNativeDeliveryCount : NativeDeliveryCount<RabbitMqTransportFactory>
{
}
using NUnit.Framework;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqTransportMessageExpiration : MessageExpiration<RabbitMqTransportFactory> { }
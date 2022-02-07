using NUnit.Framework;
using Rebus.Internals;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class TestFullyQualifiedRoutingKey
{
    [Test]
    public void CanDetermineEquality()
    {
        const string destinationAddress = "wherever@whatever";
            
        var routingKey1 = new FullyQualifiedRoutingKey(destinationAddress);
        var routingKey2 = new FullyQualifiedRoutingKey(destinationAddress);

        Assert.That(routingKey1, Is.EqualTo(routingKey2));
    }
}
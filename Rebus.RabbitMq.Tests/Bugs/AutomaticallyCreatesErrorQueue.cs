using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class AutomaticallyCreatesErrorQueue : FixtureBase
{
    [TestCase("error")]
    [TestCase("error_customized")]
    public async Task WithDefaultName(string errorQueueName)
    {
        RabbitMqTransportFactory.DeleteQueue(errorQueueName);

        Using(new QueueDeleter(errorQueueName));
    }
}
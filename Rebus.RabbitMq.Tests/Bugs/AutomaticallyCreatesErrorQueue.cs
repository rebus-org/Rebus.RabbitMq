using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class AutomaticallyCreatesErrorQueue : FixtureBase
{
    [TestCase("error")]
    [TestCase("error_customized")]
    public async Task WithDefaultName(string errorQueueName)
    {
        var inputQueueName = TestConfig.GetName("input");

        // ensure the queues do not exist beforehand
        RabbitMqTransportFactory.DeleteQueue(inputQueueName);
        RabbitMqTransportFactory.DeleteQueue(errorQueueName);

        // ensure they're cleaned up afterwards too
        Using(new QueueDeleter(inputQueueName));
        Using(new QueueDeleter(errorQueueName));

        using var activator = new BuiltinHandlerActivator();

        Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, inputQueueName))
            .Options(o =>
            {
                // pretend we didn't customize it
                if (errorQueueName == "error") return;

                o.SimpleRetryStrategy(errorQueueAddress: errorQueueName);
            })
            .Start();

        Assert.That(RabbitMqTransportFactory.QueueExists(errorQueueName), Is.True,
            $"The error queue '{errorQueueName}' was not found as expected");
    }
}
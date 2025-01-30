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
    [Description("Tried without success to reproduce an error where error queues would, for some reason, not be there after starting")]
    public async Task WhatTheFixtureSays(string errorQueueName)
    {
        var inputQueueName = TestConfig.GetName("input");

        // ensure the queues do not exist beforehand
        await RabbitMqTransportFactory.DeleteQueue(inputQueueName);
        await RabbitMqTransportFactory.DeleteQueue(errorQueueName);

        // ensure they're cleaned up afterwards too
        Using(new QueueDeleter(inputQueueName));
        Using(new QueueDeleter(errorQueueName));

        using var activator = new BuiltinHandlerActivator();

        Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, inputQueueName))
            .Options(o =>
            {
                // only works with locally running Fleet Manager, so it's commented out here
                //o.EnableFleetManager("https://localhost:44342/api",
                //    "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJhaWQiOiJ0LXRlYW0xLTEiLCJ1cG4iOiJ1c2VyMSIsIndoZW4iOiIxNjQ0MjYxMDg0Nzc4In0.-_f9REjOGM8fmZi5MyUncGoEw7XPTIVTxNCq1PDWWt5aWE5zwLAjY6WAbtU6LxUqgahmh1i-h0u9qW0FQtDv-w",
                //    new(deadLetteringStrategy: DeadLetteringStrategy.StoreInFleetManagerAndErrorQueue));

                // pretend we didn't customize it
                if (errorQueueName == "error") return;

                o.RetryStrategy(errorQueueName: errorQueueName);
            })
            .Start();

        Assert.That(await RabbitMqTransportFactory.QueueExists(errorQueueName), Is.True,
            $"The error queue '{errorQueueName}' was not found as expected");
    }
}
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
// ReSharper disable AccessToDisposedClosure

namespace Rebus.RabbitMq.Tests.Examples;

[TestFixture]
[Description("Demonstrates how Rebus+RabbitMQ can work with one single worker thread and pretty massive parallelism when doing async, Task-based work")]
public class CheckMaximumDegreeOfParallelism : FixtureBase
{
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(64)]
    [TestCase(128)]
    [TestCase(256)]
    public async Task TryParallelism(int maxParallelism)
    {
        var queueName = Guid.NewGuid().ToString("n");

        Using(new QueueDeleter(queueName));

        var messageCount = maxParallelism * 5;

        using var counter = new SharedCounter(initialValue: messageCount);

        var recordedDegrees = new ConcurrentQueue<long>();

        using var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async _ =>
        {
            await Task.Delay(millisecondsDelay: 150);
            counter.Decrement();
        });

        var bus = Configure.With(activator)
            .Logging(l => l.Console(minLevel: LogLevel.Info))
            .Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                    .Prefetch(maxNumberOfMessagesToPrefetch: 3 * maxParallelism)
                    .SetPublisherConfirms(false);
            })
            .Options(o =>
            {
                o.SetNumberOfWorkers(1);
                o.SetMaxParallelism(maxParallelism);

                o.Decorate<IPipeline>(c =>
                {
                    var pipeline = c.Get<IPipeline>();
                    var step = new ParallelismTrackingIncomingStep(recordedDegrees);
                    var position = PipelineAbsolutePosition.Front;

                    return new PipelineStepConcatenator(pipeline)
                        .OnReceive(step, position);
                });
            })
            .Start();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, messageCount),
            async (n, _) => await bus.SendLocal($"message number {n}")
        );

        counter.WaitForResetEvent(timeoutSeconds: maxParallelism * 2);

        var maxDegreeOfParallelism = recordedDegrees.Max();

        Assert.That(maxDegreeOfParallelism, Is.EqualTo(maxParallelism));
    }

    class ParallelismTrackingIncomingStep : IIncomingStep
    {
        readonly ConcurrentQueue<long> _recordedDegrees;

        long _degree;

        public ParallelismTrackingIncomingStep(ConcurrentQueue<long> recordedDegrees)
        {
            _recordedDegrees = recordedDegrees;
        }

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            try
            {
                var degree = Interlocked.Increment(ref _degree);

                _recordedDegrees.Enqueue(degree);

                await next();
            }
            finally
            {
                Interlocked.Decrement(ref _degree);
            }
        }
    }
}
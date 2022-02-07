using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

// ReSharper disable ConvertClosureToMethodGroup
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Description("Try to reproduce odd behavior. Unsuccessful, so far...")]
public class OutgoingMessagesAreNotSentWhenPipelineThrows : FixtureBase
{
    [Test]
    public async Task SeeIfThisWorks()
    {
        var senderQueueName = TestConfig.GetName("pipeline-thrower-sender");
        Using(new QueueDeleter(senderQueueName));

        var receiverQueueName = TestConfig.GetName("pipeline-thrower-receiver");
        Using(new QueueDeleter(receiverQueueName));

        var receivedTheMessageAnyway = Using(new ManualResetEvent(initialState: false));

        var senderActivator = Using(new BuiltinHandlerActivator());
        var receiverActivator = Using(new BuiltinHandlerActivator());

        senderActivator.Handle<SendShouldNeverBeReceived>(async (bus, message) =>
        {
            await bus.Send(new ShouldNeverBeReceived());
        });
            
        receiverActivator.Handle<ShouldNeverBeReceived>(async message =>
        {
            receivedTheMessageAnyway.Set();
        });

        Configure.With(receiverActivator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, receiverQueueName))
            .Start();

        var senderBus = Configure.With(senderActivator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, senderQueueName))
            .Routing(r => r.TypeBased().Map<ShouldNeverBeReceived>(receiverQueueName))
            .Options(o =>
            {
                EnableThrowingStep(o);
                o.LogPipeline(verbose: true);
            })
            .Start();

        await senderBus.SendLocal(new SendShouldNeverBeReceived());

        if (receivedTheMessageAnyway.WaitOne(timeout: TimeSpan.FromSeconds(5)))
        {
            throw new AssertionException("The message ShouldNeverBeReceived (which should never have been received) WAS received!");
        }
    }

    static void EnableThrowingStep(OptionsConfigurer configurer)
    {
        configurer.Decorate<IPipeline>(c =>
        {
            var pipeline = c.Get<IPipeline>();
            return new PipelineStepInjector(pipeline)
                .OnReceive(new ThrowingStep(), PipelineRelativePosition.Before, typeof(DispatchIncomingMessageStep));
        });
    }

    [StepDocumentation("Executes the rest of the pipeline and then immediately throws an exception.")]
    public class ThrowingStep : IIncomingStep
    {
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            await next();
            throw new ApplicationException("THIS IS THE THROWING STEP THROWING");
        }
    }

    class SendShouldNeverBeReceived { }

    class ShouldNeverBeReceived { }
}
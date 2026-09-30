using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Testcontainers.RabbitMq;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Description("A publisher confirm that never arrives must fail the send after the configured timeout instead of hanging it")]
public class PublisherConfirmsTimeoutIsHonoured : FixtureBase
{
    [Test]
    public async Task SendFailsWhenConfirmDoesNotArriveWithinTimeout()
    {
        // a dedicated broker, because blocking publishers affects every connection on it
        await using var container = new RabbitMqBuilder().Build();
        await container.StartAsync();

        var timeout = TimeSpan.FromSeconds(1);
        var activator = new BuiltinHandlerActivator();

        var bus = Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(container.GetConnectionString(), TestConfig.GetName("confirm-timeout"))
                .SetPublisherConfirms(enabled: true, timeout: timeout))
            .Options(o => o.SetNumberOfWorkers(0))
            .Start();

        try
        {
            // warm up the publisher channels while the broker still accepts publishes
            await bus.SendLocal("warm-up");

            // a zero memory watermark raises a memory alarm, which makes the broker stop reading from publishing connections
            await Exec(container, "rabbitmqctl", "set_vm_memory_high_watermark", "0");

            var stopwatch = Stopwatch.StartNew();
            var send = bus.SendLocal("never confirmed");
            var winner = await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(30)));
            stopwatch.Stop();

            Assert.That(winner, Is.SameAs(send), "The send did not fail within 30 s - the publisher confirms timeout was not honoured");

            var exception = Assert.ThrowsAsync<RebusApplicationException>(async () => await send);
            Console.WriteLine($"Send failed after {stopwatch.Elapsed.TotalSeconds:0.0} s: {exception}");
            Assert.That(exception.Message, Does.Contain("confirm"));
        }
        finally
        {
            // lift the alarm, so the bus can shut down cleanly
            await Exec(container, "rabbitmqctl", "set_vm_memory_high_watermark", "0.4");
            activator.Dispose();
        }
    }

    static async Task Exec(RabbitMqContainer container, params string[] command)
    {
        var result = await container.ExecAsync(command);
        Assert.That(result.ExitCode, Is.EqualTo(0), $"'{string.Join(" ", command)}' failed: {result.Stderr}");
    }
}

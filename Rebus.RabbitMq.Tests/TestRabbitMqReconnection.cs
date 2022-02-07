using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
[Ignore("Must be run as Administrator")]
[Description("Simulates a lost connection by restarting RabbitMQ while an endpoint is receiving messages")]
public class TestRabbitMqReconnection : FixtureBase
{
    const string ConnectionString = "amqp://localhost";
    readonly string _receiverQueueName = TestConfig.GetName("receiver");

    IBus _sender;
    BuiltinHandlerActivator _receiver;

    protected override void SetUp()
    {
        using (var transport = new RabbitMqTransport(ConnectionString, _receiverQueueName, new NullLoggerFactory()))
        {
            transport.PurgeInputQueue();
        }

        _receiver = Using(new BuiltinHandlerActivator());

        Configure.With(_receiver)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseRabbitMq(ConnectionString, _receiverQueueName).Prefetch(1))
            .Options(o =>
            {
                o.SetNumberOfWorkers(1);
                o.SetMaxParallelism(1);
            })
            .Start();

        var senderActivator = Using(new BuiltinHandlerActivator());

        _sender = Configure.With(senderActivator)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseRabbitMqAsOneWayClient(ConnectionString))
            .Routing(r => r.TypeBased().MapFallback(_receiverQueueName))
            .Start();
    }

    [Test]
    public void WeGetAllMessagesEvenThoughRabbitMqRestarts()
    {
        var messages = new ConcurrentDictionary<string, bool>();

        _receiver.Handle<string>(async message =>
        {
            Console.WriteLine($"Received '{message}'");
            await Task.Delay(500);
            messages[message] = true;
        });

        Console.WriteLine("Sending messages...");

        Enumerable.Range(0, 40)
            .Select(i => $"message number {i}")
            .ToList()
            .ForEach(message =>
            {
                messages[message] = false;
                _sender.Send(message).Wait();
            });

        Console.WriteLine("Waiting for all messages to have been handled...");

        // restart RabbitMQ while we are receiving messages
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Thread.Sleep(5000);
                Console.WriteLine("Stopping RabbitMQ....");
                Exec("net", "stop rabbitmq");
                Thread.Sleep(1000);
                Console.WriteLine("Starting RabbitMQ....");
                Exec("net", "start rabbitmq");
            }
            catch (Exception exception)
            {
                throw new AssertionException("Exception on background thread", exception);
            }
        });

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            Thread.Sleep(1000);

            if (messages.All(kvp => kvp.Value))
            {
                Console.WriteLine("All messages received :)");
                break;
            }

            var received = messages.Count(v => v.Value);
                
            Console.WriteLine($"Messages correctly received at this point: {received}");

            if (stopwatch.Elapsed < TimeSpan.FromMinutes(2)) continue;

            throw new TimeoutException("Waited too long!");
        }
    }

    static void Exec(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,

            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(startInfo);

        if (process == null)
        {
            throw new RebusApplicationException($"Could not execute '{fileName} {arguments}'");
        }

        process.WaitForExit();

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();

        Console.WriteLine(stdOut);
        Console.WriteLine(stdErr);

        if (process.ExitCode != 0)
        {
            throw new RebusApplicationException($"Exit code from application: {process.ExitCode}");
        }
    }
}
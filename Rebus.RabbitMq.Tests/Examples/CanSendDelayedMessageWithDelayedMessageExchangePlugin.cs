using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable CS1998

namespace Rebus.RabbitMq.Tests.Examples;

[TestFixture]
public class CanSendDelayedMessageWithDelayedMessageExchangePlugin : FixtureBase
{
    string _connectionString;

    protected override void SetUp()
    {
        base.SetUp();

        var container = Using(RabbitMqTestContainerManager.GetCustomContainer(
            // https://github.com/heidiks/rabbitmq-delayed-message-exchange
            builder => builder
                .WithImage("heidiks/rabbitmq-delayed-message-exchange:latest")
        ));

        _connectionString = container.ConnnectionString;
    }

    [Test]
    public async Task ShowHowItIsDone_TimeoutManager_AutomaticDeclaration()
    {
        using var gotTheMessage = new ManualResetEvent(initialState: false);

        var stopwatch = new Stopwatch();

        using var receiver = new BuiltinHandlerActivator();

        receiver.Handle<string>(async _ =>
        {
            stopwatch.Stop();
            gotTheMessage.Set();
        });

        Configure.With(receiver)
            .Transport(t => t.UseRabbitMq(_connectionString, "receiver"))
            .Routing(r => r.TypeBased().Map<string>("receiver"))
            .Timeouts(t => t.UseDelayedMessageExchange("RebusDelayed"))
            .Start();


        stopwatch.Start();
        await receiver.Bus.Defer(TimeSpan.FromSeconds(5), "HEJ MED DIG");

        gotTheMessage.WaitOrDie(timeout: TimeSpan.FromSeconds(10));

        var elapsed = stopwatch.Elapsed;

        Assert.That(elapsed, Is.GreaterThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public async Task ShowHowItIsDone_TimeoutManager()
    {
        await DeclareDelayedMessageExchange("RebusDelayed");

        using var gotTheMessage = new ManualResetEvent(initialState: false);

        var stopwatch = new Stopwatch();

        using var receiver = new BuiltinHandlerActivator();

        receiver.Handle<string>(async _ =>
        {
            stopwatch.Stop();
            gotTheMessage.Set();
        });

        Configure.With(receiver)
            .Transport(t => t.UseRabbitMq(_connectionString, "receiver"))
            .Routing(r => r.TypeBased().Map<string>("receiver"))
            .Timeouts(t => t.UseDelayedMessageExchange("RebusDelayed"))
            .Start();


        stopwatch.Start();
        await receiver.Bus.Defer(TimeSpan.FromSeconds(5), "HEJ MED DIG");

        gotTheMessage.WaitOrDie(timeout: TimeSpan.FromSeconds(10));

        var elapsed = stopwatch.Elapsed;

        Assert.That(elapsed, Is.GreaterThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public async Task ShowHowItIsDone_Manual()
    {
        await DeclareDelayedMessageExchange("RebusDelayed");

        using var gotTheMessage = new ManualResetEvent(initialState: false);

        var stopwatch = new Stopwatch();

        using var receiver = new BuiltinHandlerActivator();

        receiver.Handle<string>(async _ =>
        {
            stopwatch.Stop();
            gotTheMessage.Set();
        });

        Configure.With(receiver)
            .Transport(t => t.UseRabbitMq(_connectionString, "receiver"))
            .Routing(r => r.TypeBased().Map<string>("receiver@RebusDelayed"))
            .Start();


        stopwatch.Start();
        await receiver.Bus.Send("HEJ MED DIG", new Dictionary<string, string> { ["x-delay"] = "5000" });

        gotTheMessage.WaitOrDie(timeout: TimeSpan.FromSeconds(10));

        var elapsed = stopwatch.Elapsed;

        Assert.That(elapsed, Is.GreaterThan(TimeSpan.FromSeconds(5)));
    }

    async Task DeclareDelayedMessageExchange(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new(_connectionString) };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();

        await model.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: "x-delayed-message",
            durable: true,
            autoDelete: false,
            arguments: new Dictionary<string, object> { ["x-delayed-type"] = "direct" }
        );
    }
}
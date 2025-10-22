using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Internals;
using Rebus.Tests.Contracts;
// ReSharper disable AsyncVoidLambda
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Rebus.RabbitMq.Tests.Internals;

[TestFixture]
public class TestMiniRetrier : FixtureBase
{
    [Test]
    public async Task ItWorks()
    {
        var counter = 0;

        await MiniRetrier.ExecuteAsync(3, async () => counter++);

        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    public async Task ItWorks_Retries()
    {
        var counter = 0;

        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await MiniRetrier.ExecuteAsync(3, async () =>
        {
            counter++;
            throw new ArgumentException("oh no!");
        }));

        Console.WriteLine(ex);

        Assert.That(counter, Is.EqualTo(3));
    }
}
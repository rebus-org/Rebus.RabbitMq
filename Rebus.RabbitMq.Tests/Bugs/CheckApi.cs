using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class CheckApi
{
    [Test]
    [Explicit]
    public void AddingCustomPropertiesToCreatedEntities()
    {
        using var activator = new BuiltinHandlerActivator();

        Configure.With(activator)
            .Transport(t =>
            {
                var exchangeOptions = t.UseRabbitMq("", "").ExchangeOptions;
                
                exchangeOptions
                    .AddArgumentToDirectExchange("some-key", "some-value")
                    .AddArgumentToTopicExchange("some-key", "some-value");
            });
    }
}
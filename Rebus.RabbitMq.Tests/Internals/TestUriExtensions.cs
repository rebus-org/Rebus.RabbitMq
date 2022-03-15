using System;
using System.Web;
using NUnit.Framework;
using Rebus.Internals;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Internals;

[TestFixture]
public class TestUriExtensions : FixtureBase
{
    [TestCase("https://whatever.com")]
    [TestCase("amqp://whatever.com")]
    [TestCase("amqp://whatever:8080/bimmelimmelim")]
    public void NoCredentials(string uriString)
    {
        var uri = new Uri(uriString);

        Assert.That(uri.TryGetCredentials(out _), Is.False);
    }

    [TestCase("amqp://user:pass@localhost", "user", "pass")]
    [TestCase("amqp://user%20number%202:p%40ssword@localhost", "user number 2", "p@ssword")]
    [TestCase("amqp://this%3a+is+user%2f3:p%40ssword@localhost", "this: is user/3", "p@ssword")]
    public void YesCredentials(string uriString, string expectedUsername, string expectedPassword)
    {
        var uri = new Uri(uriString);

        Assert.That(uri.TryGetCredentials(out var credentials), Is.True);
        Assert.That(credentials.UserName, Is.EqualTo(expectedUsername));
        Assert.That(credentials.Password, Is.EqualTo(expectedPassword));
    }
}
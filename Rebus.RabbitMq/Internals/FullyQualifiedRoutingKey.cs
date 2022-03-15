using System;
using System.Linq;

namespace Rebus.Internals;

record FullyQualifiedRoutingKey
{
    public FullyQualifiedRoutingKey(string destinationAddress)
    {
        if (destinationAddress == null) throw new ArgumentNullException(nameof(destinationAddress));

        var tokens = destinationAddress.Split('@');

        if (tokens.Length > 1)
        {
            ExchangeName = tokens.Last();
            RoutingKey = string.Join("@", tokens.Take(tokens.Length - 1));
        }
        else
        {
            ExchangeName = null;
            RoutingKey = destinationAddress;
        }
    }

    public override string ToString() => ExchangeName != null ? $"{RoutingKey}@{ExchangeName}" : RoutingKey;

    public string ExchangeName { get; }
    public string RoutingKey { get; }
}
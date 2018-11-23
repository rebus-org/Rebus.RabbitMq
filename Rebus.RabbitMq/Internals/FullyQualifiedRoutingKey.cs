using System;
using System.Linq;

namespace Rebus.Internals
{
    class FullyQualifiedRoutingKey
    {
        public FullyQualifiedRoutingKey(string destinationAddress)
        {
            if (destinationAddress == null) throw new ArgumentNullException(nameof(destinationAddress));

            var tokens = destinationAddress.Split('@');

            if (tokens.Length > 1)
            {
                ExchangeName = tokens.Last();
                RoutingKey = String.Join("@", tokens.Take(tokens.Length - 1));
            }
            else
            {
                ExchangeName = null;
                RoutingKey = destinationAddress;
            }
        }

        public override string ToString() => $"{RoutingKey}@{ExchangeName}";

        public string ExchangeName { get; }
        public string RoutingKey { get; }

        protected bool Equals(FullyQualifiedRoutingKey other)
        {
            return String.Equals(ExchangeName, other.ExchangeName) && String.Equals(RoutingKey, other.RoutingKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FullyQualifiedRoutingKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ExchangeName != null ? ExchangeName.GetHashCode() : 0) * 397) ^ (RoutingKey != null ? RoutingKey.GetHashCode() : 0);
            }
        }

        public static bool operator ==(FullyQualifiedRoutingKey left, FullyQualifiedRoutingKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FullyQualifiedRoutingKey left, FullyQualifiedRoutingKey right)
        {
            return !Equals(left, right);
        }
    }
}
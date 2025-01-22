using System;
using System.Runtime.Serialization;

namespace Rebus.Exceptions;

/// <summary>
/// Exceptions that is thrown when something goes wrong while working with mandatory delivery
/// </summary>
[Serializable]
public class MandatoryDeliveryException : Exception
{
    /// <summary>
    /// Constructs the exception
    /// </summary>
    public MandatoryDeliveryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructs the exception
    /// </summary>
    public MandatoryDeliveryException(Exception innerException, string message)
        : base(message, innerException)
    {
    }
}
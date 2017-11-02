﻿using System;
using System.Runtime.Serialization;

namespace Rebus.Exceptions
{
    /// <summary>
    /// Exceptions that is thrown when something goes wrong while working with mandatory delivery
    /// </summary>
#if NET45
    [Serializable]
#endif
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

#if NET45
        /// <summary>
        /// Constructs the exception
        /// </summary>
        public MandatoryDeliveryException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}

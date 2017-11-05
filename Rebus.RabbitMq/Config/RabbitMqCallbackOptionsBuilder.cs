using System;
using RabbitMQ.Client;
using Rebus.RabbitMq;

namespace Rebus.Config
{
    /// <summary>
    /// Allows for fluently configuring RabbitMQ callbacks
    /// </summary>
    public class RabbitMqCallbackOptionsBuilder
    {
        /// <summary>
        /// Add callback function for BasicReturn event
        /// </summary>
        public RabbitMqCallbackOptionsBuilder BasicReturn(Action<object, BasicReturnEventArgs> basicReturnCallback)
        {
            BasicReturnCallback = basicReturnCallback;
            return this;
        }

        internal Action<object, BasicReturnEventArgs> BasicReturnCallback { get; private set; }
       
        internal bool HasMandatoryCallback => BasicReturnCallback != null;

        internal void ConfigureEvents(IModel model)
        {
            if (BasicReturnCallback != null)
            {
                model.BasicReturn += (sender, args) =>
                {
                    var eventArgs = new BasicReturnEventArgs()
                    {
                        Message = RabbitMqTransport.CreateTransportMessage(args.BasicProperties, args.Body),
                        Exchange = args.Exchange,
                        ReplyCode = (int)args.ReplyCode,
                        ReplyText = args.ReplyText,
                        RoutingKey = args.RoutingKey,
                    };

                    BasicReturnCallback(sender, eventArgs);
                };
            }
        }
    }
}

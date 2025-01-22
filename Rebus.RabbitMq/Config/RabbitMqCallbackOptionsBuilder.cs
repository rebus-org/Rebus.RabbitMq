using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Rebus.Logging;
using Rebus.RabbitMq;

namespace Rebus.Config;

/// <summary>
/// Allows for fluently configuring RabbitMQ callbacks
/// </summary>
public class RabbitMqCallbackOptionsBuilder
{
    /// <summary>
    /// Add callback function for BasicReturn event
    /// </summary>
    public RabbitMqCallbackOptionsBuilder BasicReturn(Func<object, BasicReturnEventArgs, Task> basicReturnCallback)
    {
        BasicReturnCallback = basicReturnCallback;
        return this;
    }

    internal Func<object, BasicReturnEventArgs, Task> BasicReturnCallback { get; private set; }
       
    internal bool HasMandatoryCallback => BasicReturnCallback != null;

    internal void ConfigureEvents(IChannel model, ILog log)
    {
        if (BasicReturnCallback != null)
        {
            model.BasicReturnAsync += async (sender, args) =>
            {
                var transportMessage = RabbitMqTransport.CreateTransportMessage(args.BasicProperties, args.Body.ToArray());

                var eventArgs = new BasicReturnEventArgs(
                    transportMessage,
                    args.Exchange,
                    args.ReplyCode,
                    args.ReplyText,
                    args.RoutingKey
                );

                await BasicReturnCallback(sender, eventArgs);
            };
        }
        else
        {
            model.BasicReturnAsync += (_, args) =>
            {
                log.Warn(
                    "Received BasicReturn event. RoutingKey: {routingKey}, exchange: {exchange}, replyCode: {replyCode}, replyText: {replyText}",
                    args.RoutingKey, args.Exchange, args.ReplyCode, args.ReplyText);
                return Task.CompletedTask;
            };
        }
    }
}
using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Rebus.Config
{
    public class RabbitMqCallbackOptionsBuilder
    {
        public RabbitMqCallbackOptionsBuilder BasicReturn(Action<object, BasicReturnEventArgs> basicReturnCallback)
        {
            BasicReturnCallback = basicReturnCallback;
            return this;
        }

        public RabbitMqCallbackOptionsBuilder CallbackException(Action<object, CallbackExceptionEventArgs> callbackExceptionCallback)
        {
            CallbackExceptionCallback = callbackExceptionCallback;
            return this;
        }

        public RabbitMqCallbackOptionsBuilder FlowControl(Action<object, FlowControlEventArgs> flowControlCallback)
        {
            FlowControlCallback = flowControlCallback;
            return this;
        }

        public RabbitMqCallbackOptionsBuilder ModelShutdown(Action<object, ShutdownEventArgs> modelShutdownCallback)
        {
            ModelShutdownCallback = modelShutdownCallback;
            return this;
        }

        internal Action<object, BasicReturnEventArgs> BasicReturnCallback { get; private set; }
        internal Action<object, CallbackExceptionEventArgs> CallbackExceptionCallback { get; private set; }
        internal Action<object, FlowControlEventArgs> FlowControlCallback { get; private set; }
        internal Action<object, ShutdownEventArgs> ModelShutdownCallback { get; private set; }

        internal bool HasMandatoryCallback => BasicReturnCallback != null;

        internal void ConfigureEvents(IModel model)
        {
            if (BasicReturnCallback != null)
            {
                model.BasicReturn += (sender, args) => BasicReturnCallback(sender, args);
            }

            if (CallbackExceptionCallback != null)
            {
                model.CallbackException += (sender, args) => CallbackExceptionCallback(sender, args);
            }

            if (FlowControlCallback != null)
            {
                model.FlowControl += (sender, args) => FlowControlCallback(sender, args);
            }

            if (ModelShutdownCallback != null)
            {
                model.ModelShutdown += (sender, args) => ModelShutdownCallback(sender, args);
            }
        }
    }
}

using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.Retry;
using Rebus.Transport;

namespace Rebus.Internals;

class RabbitMqErrorHandlerDecorator(IErrorHandler errorHandler) : IErrorHandler
{
    public async Task HandlePoisonMessage(TransportMessage transportMessage, ITransactionContext transactionContext, ExceptionInfo exception)
    {
        var clone = transportMessage.Clone();

        clone.Headers.Remove("x-delivery-count");

        await errorHandler.HandlePoisonMessage(clone, transactionContext, exception);
    }
}
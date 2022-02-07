using RabbitMQ.Client.Exceptions;

namespace Rebus.Internals;

static class OperationInterruptedExceptionExtensions
{
    public static bool HasReplyCode(this OperationInterruptedException exception, int code) =>
        exception.ShutdownReason != null && exception.ShutdownReason.ReplyCode == code;
}
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Rebus.Internals;

static class ModelExtensions
{
    /// <summary>
    /// Disposes the specific 
    /// </summary>
    internal static async ValueTask SafeDropAsync(this IChannel model)
    {
        if (model == null)
        {
            return;
        }
            
        try
        {
            await model.CloseAsync();
            await model.DisposeAsync();
        }
        catch 
        {
            // it's so fucked up that these can throw exceptions
        }
    }
}
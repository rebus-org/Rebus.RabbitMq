using System;
using System.Threading.Tasks;

namespace Rebus.Internals;

class MiniRetrier
{
    public static async ValueTask ExecuteAsync(int attempts, Func<Task> function)
    {
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Please make at least 1 attempt");

        var counter = 0;

        while (true)
        {
            try
            {
                await function();
                return;
            }
            catch (Exception) when (++counter < attempts)
            {
            }
        }
    }
}
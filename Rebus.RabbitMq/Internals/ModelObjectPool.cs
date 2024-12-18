using System;
using System.Collections.Concurrent;
using System.Threading;
using RabbitMQ.Client;

namespace Rebus.Internals;

/// <summary>
/// A simple object pool implementation, because the default (Microsoft.Extensions.ObjectPool)
/// doesn't allow proper dispose methods
/// </summary>
class ModelObjectPool : IDisposable
{
    readonly ConcurrentBag<IModel> _availableObjects = new();
    readonly WriterModelPoolPolicy _policy;

    int _maxEntries;
    int _currentCount = 0; 

    public ModelObjectPool(WriterModelPoolPolicy policy, int maxEntries)
    {
        _policy = policy;
        _maxEntries = maxEntries;
    }

    public void SetMaxEntries(int maxEntries)
    {
        _maxEntries = maxEntries;
    }

    public IModel Get()
    {
        if (_availableObjects.TryTake(out var model))
        {
            Interlocked.Decrement(ref _currentCount);
            return model;
        }

        return _policy.Create();
    }

    public void Return(IModel model)
    {
        if (Interlocked.Increment(ref _currentCount) > _maxEntries)
        {
            Interlocked.Decrement(ref _currentCount);
            model.SafeDrop();
        }
        else
        {
            _availableObjects.Add(model);
        }
    }

    public void Dispose()
    {
        foreach (var model in _availableObjects)
        {
            model.SafeDrop();
        }
    }
}
using System;
using System.Collections.Concurrent;
using RabbitMQ.Client;
using Rebus.RabbitMq;

namespace Rebus.Internals
{
    /// <summary>
    /// A simple object pool implementation, because the default (Microsoft.Extensions.ObjectPool)
    /// doesn't allow proper dispose methods
    /// </summary>
    internal class ModelObjectPool : IDisposable
    {
        private readonly WriterModelPoolPolicy _policy;
        internal int MaxEntries;
        private readonly ConcurrentBag<IModel> _availableObjects = new();

        internal ModelObjectPool(WriterModelPoolPolicy policy, int maxEntries)
        {
            _policy = policy;
            MaxEntries = maxEntries;
        }

        internal IModel Get()
        {
            if (_availableObjects.TryTake(out var model))
            {
                return model;
            }

            return _policy.Create();
        }

        internal void Return(IModel model)
        {
            if (_availableObjects.Count >= MaxEntries)
            {
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
}
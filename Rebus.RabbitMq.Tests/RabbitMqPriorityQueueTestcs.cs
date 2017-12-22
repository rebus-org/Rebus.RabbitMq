﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests
{
    [TestFixture, Category("rabbitmq")]
    public class RabbitMqPriorityQueueTestcs : FixtureBase
    {
        private readonly string _priorityQueueName = TestConfig.GetName("priority-queue");

        protected override void SetUp()
        {
            RabbitMqTransportFactory.DeleteQueue(_priorityQueueName);
        }

        [Test]
        public void PriorityInputQueueCreate()
        {
            // Start a server with priority
            StartServer(_priorityQueueName, 10);

            // Check if queues exists
            Assert.DoesNotThrow(() =>
            {
                var connectionFactory = new ConnectionFactory { Uri = RabbitMqTransportFactory.ConnectionString };

                using (var connection = connectionFactory.CreateConnection())
                using (var model = connection.CreateModel())
                {
                    // Throws exception if queue paramters differ
                   model.QueueDeclare(_priorityQueueName,
                        exclusive: false,
                        durable: true,
                        autoDelete: false,
                        arguments: new Dictionary<string, object>
                        {
                            {"x-max-priority", 10}
                        });
                }
            });
        }

        [Test]
        public void PriorityInputQueueCreateThrowsOnDifferentPriority()
        {
            // Start a server with priority
            StartServer(_priorityQueueName, 10);

            Assert.Throws<OperationInterruptedException>(() =>
            {
                var connectionFactory = new ConnectionFactory { Uri = RabbitMqTransportFactory.ConnectionString };

                using (var connection = connectionFactory.CreateConnection())
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(_priorityQueueName,
                        exclusive: false,
                        durable: true,
                        autoDelete: false,
                        arguments: new Dictionary<string, object>
                        {
                            {"x-max-priority", 1}
                        });
                }
            });
        }

        [Test]
        public void MultipleServersThrowsOnDifferentPriority()
        {
            StartServer(_priorityQueueName, 10);

            // Rebus throws resolution exception
            // NOTE: Would be nice if this could be a specific RebusApplicationException
            Assert.Throws<ResolutionException>(() =>
            {
                StartServer(_priorityQueueName, 1);
            });
        }

        [Test]
        public async Task PriorityQueue()
        {
            // Setup priority constants
            const int maxPriority = 10;
            const int expectedMessageCount = 30;

            var handledSequence = new List<int>();
            var gotMessages = new ManualResetEvent(false);

            var activator = StartServer(_priorityQueueName, maxPriority).Handle<string>(async str =>
            {
                Console.WriteLine($"Message arrived: {str}");

                var prio = int.Parse(str);
                handledSequence.Add(prio);

                if (handledSequence.Count == expectedMessageCount)
                {
                    gotMessages.Set();
                }
            });

            var bus = StartOneWayClient();

            // Random priority
            var rnd = new Random();
            for (var i = 0; i <= 9; i++)
            {
                var priority = rnd.Next(0, 10);
                var message = priority.ToString();
                Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

                await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
                {
                    [RabbitMqHeaders.Priority] = priority.ToString(),
                    [RabbitMqHeaders.DeliveryMode] = "1" // transient
                });
            }

            // Low priority first
            for (var i = 0; i <= 9; i++)
            {
                var priority = i;
                var message = priority.ToString();
                Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

                await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
                {
                    [RabbitMqHeaders.Priority] = priority.ToString(),
                    [RabbitMqHeaders.DeliveryMode] = "1"
                });
            }

            // High priority first
            for (var i = 0; i <= 9; i++)
            {
                var priority = 9-i;
                var message = priority.ToString();
                Console.WriteLine($"Sending '{message}' message to '{_priorityQueueName}'");

                await bus.Advanced.Routing.Send(_priorityQueueName, message, new Dictionary<string, string>
                {
                    [RabbitMqHeaders.Priority] = priority.ToString(),
                    [RabbitMqHeaders.DeliveryMode] = "1"
                });
            }

            activator.Bus.Advanced.Workers.SetNumberOfWorkers(1);

            Console.WriteLine("Waiting for all message to arrive");

            gotMessages.WaitOrDie(TimeSpan.FromSeconds(5));

            Console.WriteLine("Got all messages :)");

            var error = false;
            int? last = null;
            foreach (var prio in handledSequence)
            {
                Console.WriteLine($"Sequence: {prio}");
                if (last.HasValue && last.Value < prio)
                {
                    error = true;
                }

                last = prio;
            }

            Assert.AreEqual(expectedMessageCount, handledSequence.Count, $"Expected {expectedMessageCount} messages");
            Assert.False(error, "Sequence is out of order");
        }

        private BuiltinHandlerActivator StartServer(string queueName, int maxPriority)
        {
            var activator = Using(new BuiltinHandlerActivator());

            Configure.With(activator)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                    .StrictPriorityQueue(maxPriority))
                .Options(o =>
                {
                    o.SetNumberOfWorkers(0);
                }).Start();

            return activator;
        }

        private IBus StartOneWayClient()
        {
            var client = Using(new BuiltinHandlerActivator());

            return Configure.With(client)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqTransportFactory.ConnectionString))
                .Start();
        }
    }
}

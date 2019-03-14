using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rebus.RabbitMq.Tests
{
    [TestFixture]
    public class RabbitMqCreateQueueTest : FixtureBase
    {
        readonly string _testQueue1 = TestConfig.GetName("test_queue_1");

        protected override void SetUp()
        {
            RabbitMqTransportFactory.DeleteQueue(_testQueue1);
        }

        [Test]
        public void Test_CreateQueue_WHEN_InputQueueOptions_AutoDelete_False_AND_TTL0_THEN_BusCanStart()
        {
            var activator = Using(new BuiltinHandlerActivator());
            var configurer = Configure.With(activator)
                  .Transport(t =>
                  {
                      t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, _testQueue1)
                        .InputQueueOptions(o => o.SetAutoDelete(false))
                        .AddClientProperties(new Dictionary<string, string> {
                            { "description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs" }
                        });
                  });
            var bus = configurer.Start();

            Assert.IsTrue(bus.Advanced.Workers.Count > 0);
        }


        [Test]
        public void Test_CreateQueue_WHEN_InputQueueOptions_AutoDelete_True_AND_TTL_Positive_THEN_BusCanStart()
        {
            var activator = Using(new BuiltinHandlerActivator());
            var configurer = Configure.With(activator)
                  .Transport(t =>
                  {
                      t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, _testQueue1)
                        .InputQueueOptions(o => o.SetAutoDelete(true, 1))
                        .AddClientProperties(new Dictionary<string, string> {
                            { "description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs" }
                        });
                  });

            var bus = configurer.Start();

            Assert.IsTrue(bus.Advanced.Workers.Count > 0);
        }

        [Test]
        public void Test_CreateQueue_WHEN_InputQueueOptions_AutoDelete_True_AND_TTL_0_THEN_ArgumentExceptionThrown()
        {
            var activator = Using(new BuiltinHandlerActivator());

            Assert.Throws<ArgumentException>(() =>
            {
                var configurer = Configure.With(activator)
                    .Transport(t =>
                    {
                        t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, _testQueue1)
                        .InputQueueOptions(o => o.SetAutoDelete(true, 0))
                        .AddClientProperties(new Dictionary<string, string> {
                            { "description", "CreateQueue_With_AutoDelete test in RabbitMqCreateQueueTest.cs" }
                        });
                  });
            }
            , "Time must be in milliseconds and greater than 0");
        }
    }
}

# Changelog

## 2.0.0-a1
* Test release

## 2.0.0-a2
* Update RabbitMQ client dependency to 4.0.1
* Update target framework to .NET 4.5.2

## 2.0.0-b01
* Test release

## 2.0.0-b02
* More convenient namespaces

## 2.0.0
* Release 2.0.0

## 3.0.0
* Update to Rebus 3

## 4.0.0
* Update to Rebus 4
* Add .NET Core support (netstandard1.5)

## 4.1.0
* Change node failover strategy to rely on built-in capabilities of the driver - thanks [samartzidis]

## 4.1.1
* Fix credentials issue - thanks [samartzidis]

## 4.1.2
* Fix weird dependency on NUnit test adapter - thanks [bzuu]

## 4.2.0
* Add support for priority queues (requires RabbitMQ 3.5) - thanks [K3llr]
* Ability to configure SSL settings - thanks [nebelx]

## 4.3.0
* Additional configuration overloads to allow for passing custom connection information to the RabbitMQ connection factory - thanks [nebelx]

## 4.4.0
* Update RabbitMQ client dependency to v. 5.0.1 - thanks [dougkwilson]

## 4.4.1
* Handle `SecurityException` when trying to figure out which machine we're running on - thanks [samartzidis]

## 4.4.2
* Minor tweak to avoid potential dictionary trouble with double-adding - thanks [samartzidis]

## 5.0.0
* Add callback to allow for customizing the `IConnectionFactory` instance used by the transport
* Make assembly non-CLS compliant (necessary to be able to customize RabbitMQ's connection factory)
* Automatically create input queue if it suddenly disappears while the app is running - thanks [pjh1974]
* Change how models are managed to maximise reuse and improve performance
* Don't log that silly `EndOfStreamException`, because that's apparently how the RabbitMQ driver rolls...
* Add support for enabling publisher confirms - thanks [hansehe]
* Complete support for publishing on alternate exchanges via the `@` syntax (topics like `<topic-name>@<exchange-name>`) - thanks [hansehe]

## 5.0.1
* Improve publisher confirms performance when sending batches (i.e. multiple outgoing messages from Rebus handlers, or when using `RebusTransactionScope`) - thanks [kyrrem]

## 5.0.2
* Add user ID header to incoming message if set on RabbitMQ transport message - thanks [michalsteyn]

## 5.1.0
* Add ability to set auto-delete TLL when enabling it - thanks [ronnyek]

## 5.1.1
* Fix "invalid arg 'x-expires' for queue" error - thanks [jarikp]

## 5.1.2
* Little fix that enables graceful handling of disappearing input queue - thanks [jarikp]

## 5.1.3
* Fix auto-delete support, which would make it impossible to configure queue TTL unless auto-delete was ON (but these things are not dependent) - thanks [jarikp]
* Fix connection name feature - thanks [jarikp]

## 5.2.0
* Decode MIME type of Rebus' `rbs2-content-type` header and pass as separate content type/encoding headers in RabbitMQ props

## 6.0.0
* Update to Rebus 6 - thanks [rsivanov]

## 6.1.0
* Add support for subscribing to exchange-qualified topics via the `topic@exchange` syntax, so you can e.g. `await bus.Advanced.Topics.Subscribe("some-topic@some-exchange")`

## 7.0.0
* Enable publisher confirms by default, because otherwise RabbitMQ can LOSE your messages after you publish them! Use `RebusTransactionScope` to batch send operations to be able to send faster, reliably, OR add Rebus' `rbs2-express` header (via `Headers.Express`) to easily opt out of end-to-end durability alltogether.

## 7.1.0
* Add explicit support for handling of separate RabbitMQ correlation ID, passed to Rebus as `rabbitmq-corr-id`

## 7.1.1
* Get rid of closed connections quicker, when there has been a connection outage

## 7.2.0
* Add ability to set queue options to use when creating queues other than the input queue. Useful to ensure that an automatically declared error queue has some specific properties applied to it.

## 7.3.0
* Update RabbitMQ client to 6.0 - thanks [mathiasnohall]

## 7.3.2
* Add code to manually transfer basic auth credentials from connection string to connection factory
* In RabbitMqTransport.Receive call QueueDeclarePassive only if declare input queue is true - thanks [marcoariboni]
* Use `Stopwatch` instead of `DateTime.Now` in `SharedQueue` to do the timeout math
* More push-based operations - thanks [zlepper]
* Truncate header values when they're too big to fit - thanks [zlepper]
* Detect errors in connections used for outgoing messages and throw them out if they fail
* Add another channel re-initialization case - thanks [zlepper]

## 7.3.3
* Remove useless logging around dequeueing operation

## 7.3.4
* Block on empty queue instead of returning NULL after a few seconds.

## 7.3.5
* Support basic URL-encoded credentials in URI

## 7.4.1
* Make publisher confirms timeout configurable via an additional `EnablePublisherConfirms` overload

## 7.4.2
* Small optimizations to reduce the amount of work done when mapping headers

## 7.4.3
* Update RabbitMq.Client dependency to 6.4.0

## 7.4.4
* Avoid creating a RabbitMQ connection during initialization, if there's no need for it because all declarations have been disabled

## 7.4.5
* Enable retries when trying to create a queue

## 7.4.6
* Add experimental support for sending deferred messages using the RabbitMQ Delayed Message Exchange plugin (use `.Timeouts(t => t.UseDelayedMessageExchange("RebusDelayed"))`)

## 8.0.0
* Update Rebus dependency to 7

## 8.1.0
* Correct how hostnames are passed to RabbitMQ's connection factory to enable proper failover

## 9.0.1
* Update to Rebus 8
* Change SSL protocol default to NONE (i.e. leave it to the .NET framework or the operating system) - thanks [MrAdam]
* Don't set URI on RabbitMQ's own connection factory to have it correctly be able to fail over when running with multiple nodes
* Make 1/100th as many defensive calls to `QueueDeclarePassive` in the transport's `Receive` method
* Fix bug that would accidentally use a /-prefixed virtual host when a virtual host was actually specified in the URI
* Update RabbitMq.Client to 6.6.0

## 9.1.0
* Implement support for providing the delivery count from `x-delivery-count` when available (which is is on quorum queues)
* Fix passing of SSL options - thanks [yuriyostapenko]
* Update to Rebus 8.2.2

## 9.2.0
* Add optional batching, which can be enabled by calling `.SetBatchSize(n)` on the RabbitMQ configuration builder

## 9.2.1
* Fix bug that would accidentally use the `@`-prefixed exchange name when declaring it

## 9.3.0
* Enable setting the consumer tag - thanks [savissimo]

## 9.3.1
* Get delivery count with `BitConverter` if the header value is `byte[]`

## 9.3.6
* Interpret delivery count header value as ASCII text with a number, when the type encountered is `byte[]`
* Ensure that RabbitMQ's built-in quorum queue header `x-delivery-count` is cleared when a message is dead-lettered

## 9.4.0
* Screen the `IModel` for fitness before trying to send with it, and perform send operations with up to 3 attempts to hopefully better overcome transient errors

## 9.4.1
* Fix potential race condition in ModelObjectPool - thanks [mksergiy]

## 10.0.0
* Update RabbitMQ client to v7 - thanks [zlepper]

## 10.0.1
* Fix Rebus.RabbitMQ not noticing if queues has been created after the initial check failed - thanks [zlepper]

---

[bzuu]: https://github.com/bzuu
[dougkwilson]: https://github.com/dougkwilson
[hansehe]: https://github.com/hansehe
[jarikp]: https://github.com/jarikp
[K3llr]: https://github.com/K3llr
[kyrrem]: https://github.com/kyrrem
[marcoariboni]: https://github.com/marcoariboni
[mathiasnohall]: https://github.com/mathiasnohall
[michalsteyn]: https://github.com/michalsteyn
[mksergiy]: https://github.com/mksergiy
[MrAdam]: https://github.com/MrAdam
[nebelx]: https://github.com/nebelx
[pjh1974]: https://github.com/pjh1974
[ronnyek]: https://github.com/ronnyek
[rsivanov]: https://github.com/rsivanov
[samartzidis]: https://github.com/samartzidis
[savissimo]: https://github.com/savissimo
[yuriyostapenko]: https://github.com/yuriyostapenko
[zlepper]: https://github.com/zlepper

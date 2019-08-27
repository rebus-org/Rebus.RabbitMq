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

---

[bzuu]: https://github.com/bzuu
[dougkwilson]: https://github.com/dougkwilson
[hansehe]: https://github.com/hansehe
[jarikp]: https://github.com/jarikp
[K3llr]: https://github.com/K3llr
[kyrrem]: https://github.com/kyrrem
[michalsteyn]: https://github.com/michalsteyn
[nebelx]: https://github.com/nebelx
[pjh1974]: https://github.com/pjh1974
[ronnyek]: https://github.com/ronnyek
[samartzidis]: https://github.com/samartzidis

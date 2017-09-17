# Rebus.RabbitMq

[![install from nuget](https://img.shields.io/nuget/v/Rebus.RabbitMq.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.RabbitMq)

Provides a RabbitMQ transport implementation for [Rebus](https://github.com/rebus-org/Rebus).

![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---

# For RabbitMQ, running on a Docker for Windows instance.

Install [Docker for windows](https://www.docker.com/docker-windows)

Build and run your RabbitMQ with management console, with the following shell command:
```
docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 8080:15672 rabbitmq:3-management
```

From your web brower, navigate to the RabbitMQ management console,

```
http://localhost:8080
```
and then login with the default credentials that were automatically configured for the Docker instance.
```
username: guest
password: guest
```
In Visual Studio 2017, run All Tests form the menus: Tests -> Run -> All Tests. While test are running, switch back to the management console, and watch:

![](https://github.com/jonmat/Rebus.RabbitMq/blob/master/rabbit-mgmt-console.png)



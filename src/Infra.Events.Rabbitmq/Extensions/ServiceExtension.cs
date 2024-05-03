﻿using System.Reflection;
using Autofac;
using Infra.Eevents;
using Infra.Events.Kafka;
using MassTransit;
using Event = Domain.Event;

namespace Infra.Events.Rabbitmq;

public static class ServiceExtension
{
    public static void AddRabbitmqInternal(
        ContainerBuilder builder,
        Action<RabbitmqOptions> rabbitmqConfigurator,
        Action<RabbitmqConsumerConfig> consumerConfigurator)
    {
        // Consumer
        Guard.NotNull(consumerConfigurator, nameof(consumerConfigurator));
        var consumerConfig = new RabbitmqConsumerConfig();
        consumerConfigurator(consumerConfig);

        Guard.NotNull(consumerConfigurator, nameof(consumerConfigurator));
        var rabbitmqConfigs = new RabbitmqOptions();
        rabbitmqConfigurator(rabbitmqConfigs);

        var eventInfos = consumerConfig.ExtractAssemblies();

        builder
            .UseMassTransitPublisherAndConsumer(rabbitmqConfigs, eventInfos);

        builder
            .RegisterType<MassTransitEventBus>().As<IEventBus>().SingleInstance();

        builder
            .RegisterAssemblyTypes(consumerConfig.EventAssemblies)
            .AsClosedTypesOf(typeof(IMessageHandler<>))
            .AsImplementedInterfaces()
            .InstancePerDependency();
    }

    private static void UseMassTransitPublisherAndConsumer(
        this ContainerBuilder builder,
        RabbitmqOptions config,
        List<RabbitMqTransportInfo> eventInfos)
    {
        //services.AddScoped<IMassTransitPublisher<>, MassTransitPublisher<>>();

        builder.AddMassTransit(bus =>
        {
            bus.AddConsumer<MassTransitConsumer>();

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(config.Host), host =>
                {
                    host.Username(config.Username);
                    host.Password(config.Password);
                });

                foreach (var eventInfo in eventInfos)
                {
                    //configure publishers
                    cfg.Publish<Event>(x =>
                    {
                        x.Durable = true;
                        x.AutoDelete = false;
                        x.ExchangeType = eventInfo.ExchangeType.ToString();
                    });
                    cfg.Message<Event>(x => x.SetEntityName(eventInfo.ExchangeName));

                    //configure consumers: bind an exchange to a receive endpoint:
                    cfg.ReceiveEndpoint(queueName: eventInfo.QueueName, endpoint =>
                    {
                        endpoint.Durable = true;
                        endpoint.AutoDelete = false;
                        endpoint.Bind(exchangeName: eventInfo.ExchangeName, exchangeCfg =>
                        {
                            exchangeCfg.Durable = true;
                            exchangeCfg.AutoDelete = false;
                            exchangeCfg.ExchangeType = eventInfo.ExchangeName;
                            exchangeCfg.RoutingKey = eventInfo.RoutingKey;
                        });
                    });
                }
            });
        });
    }

    private static List<RabbitMqTransportInfo> ExtractAssemblies(this RabbitmqConsumerConfig consumerConfig)
    {
        var events = consumerConfig.EventAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo<Event>());

        var eventInfos = new List<RabbitMqTransportInfo>();

        foreach (var eventType in events)
        {
            if (eventType.GetConstructors().All(c => c.GetParameters().Count() != 0))
            {
                continue;
            }

            var handlerType = typeof(IMessageHandler<>).MakeGenericType(eventType);
            var hasHandler = consumerConfig
                .EventAssemblies
                .Any(ass => ass.GetTypes().Any(ty => handlerType.IsAssignableFrom(ty)));

            if (hasHandler)
            {
                var queueAttribute = eventType.GetCustomAttribute<QueueAttribute>();
                var rabbitmqExchangeInfo = queueAttribute != null
                    ? new RabbitMqTransportInfo(queueAttribute.ExchangeName, queueAttribute.QueueName,
                        queueAttribute.ExchangeType, queueAttribute.RoutingKey)
                    : new RabbitMqTransportInfo(eventType.FullName, eventType.FullName);

                eventInfos.Add(rabbitmqExchangeInfo);
            }
        }

        return eventInfos;
    }
}
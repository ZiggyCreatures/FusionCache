var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache-redis").WithRedisInsight();
var serviceBus = builder
	.AddAzureServiceBus("cache-servicebus")
	.RunAsEmulator(c =>
	{
		c.WithLifetime(ContainerLifetime.Persistent);
		c.WithContainerName("cache_serviceBus");
	});

var topic = serviceBus.AddServiceBusTopic("fusioncache-playground");
topic.AddServiceBusSubscription("webApp1-sub");
topic.AddServiceBusSubscription("webApp2-sub");

builder
	.AddProject<Projects.WebApplication1>("webapplication1")
	.WithReference(redis)
	.WithReference(serviceBus).WaitFor(serviceBus);

builder
	.AddProject<Projects.WebApplication2>("webapplication2")
	.WithReference(redis)
	.WithReference(serviceBus).WaitFor(serviceBus);

builder.Build().Run();

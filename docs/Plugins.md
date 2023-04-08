<div align="center">

![FusionCache logo](logo-plugin-128x128.png)

</div>

# :jigsaw: Plugins

FusionCache supports extensibility via plugins: it is possible for example to listen to [events](Events.md) and react in any way you want.

In time, the most useful plugins will be listed directly in the homepage.


## How to create a plugin

Simply write a class that implements the [`IFusionCachePlugin`](https://github.com/jodydonetti/ZiggyCreatures.FusionCache/blob/main/src/ZiggyCreatures.FusionCache/Plugins/IFusionCachePlugin.cs) interface, which basically boils down to implement just the `Start` and `Stop` methods: from there you can setup your custom logic and do your thing.

Of course it can also accept its own set of options, typically modelled via `IOptions<MyPluginType>` and friends.

If you like there's a [:jigsaw: complete sample](PluginSample.md) available.


## How to use a plugin via DI

As explained in the [Builder](DependencyInjection.md) docs, we have 2 ways:

- register our plugin in the DI container (better before registering FusionCache itself) to respond to the `IFusionCachePlugin` service (either as singleton or transient, depending on the specific plugin) and tell FusionCache to use all registered plugins
- directly specify to use a plugin via an instance or a factory

The first approach looks like this:

```csharp
services.AddSingleton<IFusionCachePlugin, MyPlugin>();

services.AddFusionCache()
  .WithAllRegisteredPlugins()
;
```

The second approach looks like this:

```csharp
services.AddFusionCache()
  .WithPlugin(new MyPlugin(...))
;
```

In both cases, when a FusionCache instance will be created by the DI container, the plugins will be added (and started) automatically.

Of course you can also define your own custom ext method to be a better .NET citizen (see the full example below).

```csharp
services.AddSingleton<IFusionCachePlugin, MyPlugin>();

services.AddFusionCache();
```

or with your own custom ext method (like `AddMyFusionCachePlugin`) and custom options:

```csharp
services.AddMyFusionCachePlugin(options => {
  options.Whatever = 42;
});

services.AddFusionCache()
  .WithAllRegisteredPlugins()
;
```

It goes without saying, but you can register multiple plugins, all responding to the same `IFusionCachePlugin` service, and they will all be picked up by FusionCache when needed:

```csharp
services.AddSingleton<IFusionCachePlugin, MyFirstPlugin>();
services.AddSingleton<IFusionCachePlugin, MySecondPlugin>();

services.AddFusionCache()
  .WithAllRegisteredPlugins()
;
```


## How to use a plugin without DI
Just create an instance of your plugin and pass it to the `cache.AddPlugin` method.

**Example:**

```csharp
var firstPlugin = new MyFirstPlugin();
var secondPlugin = new MySecondPlugin();

var cache = new FusionCache(new FusionCacheOptions());

cache.AddPlugin(firstPlugin);
cache.AddPlugin(secondPlugin);
```

:warning: Remember that, in case the `Start` method of your plugin throws and exception, an `InvalidOperationException` would also be thrown by the `AddPlugin` method itself (with the original exception as the inner one).

Instead, in the DI way, this is already taken care of and no unhandled exception will be thrown.

In both cases the original exception will be logged.


## How to remove a plugin

Normally there's no need to remove a plugin manually: if you just want to clean things up "at the end" (and you should do it) you don't have to do anything because when a `FusionCache` instance is disposed, all added plugins will be automatically stopped and removed.

But if, for whatever reason, you need to keep using a FusionCache instance after removing a particular plugin you've previously added, you can call the `cache.RemovePlugin` method: it will automatically call the `Stop` method of the plugin and then remove it.


## A practical example

Want to follow a complete, end-to-end example to create your first plugin? There's one available [:jigsaw: right here](PluginSample.md).
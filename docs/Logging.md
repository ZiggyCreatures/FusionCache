<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 📜 Logging

Sometimes things go bad, and when that happens we go into detective mode to try to figure out what is going on: at that moment any help investigating would be very helpful, and logging is key to that.

FusionCache uses the standard `ILogger<T>` interface and a structured logging approach so it fits well in the .NET ecosystem allowing us to use any implementation we want, as long as it respects the [standard way](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging) of logging in .NET, so we should feel at home working with it.

In general logging in .NET boils down to this:

1. library: choose an actual logger implementation that we like: there's the native .NET one and there are others more advanced, like [Serilog](https://serilog.net/), [NLog](https://nlog-project.org/) and more
2. sinks: every logger implementation can then write to one or more sinks, which are actual "log destinations" where log events will be sent to. Examples can be the console, local files, a remote logging aggregation service, etc
3. minimum level (default): specify a global/defualt minimum log level that will be used as a "filter". Every log event lower than that will be ignored
4. minimum level (overrides): optionally specify different min levels per component (via namespaces, see below), in case we want to log less/more than the default, but only for specific components

In FusionCache there are also some advanced configurations, like which log levels to use for different concerns (see below).

## 📦 How to specify a logger

As is common in .NET components, the `FusionCache` constructor can accept an `ILogger<FusionCache>` param to specify which logger instance to use.

If we are instantiating a FusionCache instance manually (eg: `new FusionCache(...)`) we can pass an instance of any class that implements the `ILogger<T>` interface and it will use it. Of course we can also avoid passing it at all (or pass `null`) so no logging will happen.

The same is true when using [Dependency Injection](DependencyInjection.md), and that will work automatically with the registered logger we've choosen to use.

There are native logger implementations available in .NET itself, or we can just grab one of the many Nuget packages available like the forementioned Serilog, NLog, etc.

In general each logger has its own way to setup itself, so we should look at their docs for how to do that.

As said FusionCache follows the official .NET logging guidelines, and that includes [configuration](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging#configure-logging).

| ℹ NOTE |
| :--- |
| A bit of nitpicking here, but to be precise FusionCache does not know anything about logging configuration per se, it just uses "a standard logger" and everything works just fine because it is the logger itself that is configured. We'll see here how to configure a logger, but just as a way to get started with logging, even though that is not specific to FusionCache. |

## 🎚 Standard configuration

Here's an example of a logging configuration in the standard `appsettings.json` file, for the native .NET loggers and with a couple of min level overrides:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Warning",
            "MyCompany": "Information",
            "ZiggyCreatures.Caching.Fusion": "Debug"
        }
    }
}
```

If we are using a different logger we should look at its own documentation because it may have a specific way to configure which sinks/providers to use and their minimum log levels.

For example by saying `"MyCompany": "Information"` we are saying that any log event sent by a component in that namespace will be filtered based on the level specified: in this specific case if we log something from a class in the `MyCompany` namespace, like from an instance of a `MyCompany.MyComponent` class with a `Warning` level it will be actually logged because `Warning` is higher than `Information` but if we try to log something with a `Debug` level it will be ignored, because `Debug` is lower than `Information`, and so on.

Here's the same example as above but for [Serilog](https://serilog.net/), which uses a different config structure:

```json
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Warning",
            "Override": {
                "MyCompany": "Information",
                "ZiggyCreatures.Caching.Fusion": "Debug"
            }
        }
    }
}
```

In both cases it basically boils down to specify a global minimum log level, which serves as a default, and optionally specify some *overrides* for each component we want to customize: that is done via each class *namespace*.

Again, we can read more about this in the [official documentation](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging#configure-logging).

In general though the nice thing about this is that once we've choosen the logger we want to use and have configured it, everythig will work the same for all of our components and libraries, like FusionCache (if they respect the standard logging flow of course!).

## ⚡ Advanced configuration

Most of the times it is clear which `LogLevel` to use for something: a piece of code that should've run normally throws instead an exception? We catch it and log the event with a `Error`, including the exception itself and re-throw, so the library users can handle it the way they want. Easy peasy.

Other times though it's more difficult, and some errors may not be seen as really problematic as others, and logging every error with a `Error` is not the right call.

Even more than that the log level to use is not necessarily fixed in stone, there is not a "correct one" for everybody: different users of a library may have different needs.

As an example, in FusionCache if we use a 2nd level cache there may be errors while reading from/writing to it: being a secondary level, it's not right to log that as an `Error` since such errors did not block the normal flow, and that is why by default they are logged as `Warning`. That may be fine, but if we know that our distributed cache sometimes has problems we may not want to log a warning everytime (so we may have preferred a `Information`). On the other hand, maybe we know our distributed cache is in perfect shape, and we want to be warned immediately about any problem there (so we may have preferred a `Error`).

Now, we may be thinking about the min levels configuration we talked about above, right? Nope, that would not cut it.

If we specify a min level of `Error` we would loose **all** the warnings, and if we specify a min level of `Inforomation` we would get **all** the warnings, and in both case that may not be ok.

To solve this FusionCache let us specify, for a small selection of concerns, which `LogLevel` to use: we can do that by setting some of the options in the `FusionCacheOptions` class.

For example:

- `SerializationErrorsLogLevel` (default: `Error`): errors while serializing/deserializing data /typically while talking to the distributed cache)
- `DistributedCacheSyntheticTimeoutsLogLevel` (default: `Warning`): when soft/hard timeouts actually occurs while talking to the distributed cache
- `DistributedCacheErrorsLogLevel` (default: `Warning`): when any other error occurs while talking to the distributed cache
- `FactorySyntheticTimeoutsLogLevel` (default: `Warning`): when soft/hard timeouts actually occurs while executing the factory
- `FactoryErrorsLogLevel` (default: `Warning`): when any other error occurs while executing the factory
- and more

We can see all of them [here](Options.md) in the Options docs.

## 🕵️‍♀️ Example

Let's say we want to know about all the problems related to calling the database in our factory calls, except for when synthetic timeouts occur (eg: because we set a soft timeout to a very low `10 ms` value, so it will be hit frequently). Also suppose we set our configuration with a min level of something like `Information` or `Warning`.

We should simply this:

```csharp
options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
options.FactoryErrorsLogLevel = LogLevel.Error;
```

From now on we will never see log entries for synthetic timeouts, leaving our logs with way less background noise, on top of consuming less log storage 🎉

## ⛱ Playground

In case we want to get more familiar with FusionCache and logging, the **Playground** can help us make sense of it with some concrete examples by changing some code and looking at the actual console output.

If we download FusionCache source code or clone the repo we can see it has a `tests` folder which contains 2 projects, one of which is `ZiggyCreatures.FusionCache.Playground`.

This is a sample console app with different scenarios to play with, one of which is called `LoggingScenario`: it is an example of how to set up a logger (it contains both the standard .NET console logger and a Serilog console logger) and it consists of a series of simple operations done on a FusionCache instance. By running it we can see what will be logged for every operation, and by playing with the minimum log levels in the configuration and the FusionCache [logging options](Options.md) we can fine tune what we want to log, to avoid logging too much or too little.

By setting the min level to `Trace`/`Verbose` a lot of information will be logged, including attempts to read from the memory cache and low-level internal decisions taken by FusionCache, so we can better understand what has happened.

Here is such an output:

```
[12:13:45 DBG] FUSION (O=1e5b5fbb84fa4df89c1f18e6869e1899 K=foo): calling GetOrDefaultAsync<T> null
[12:13:45 VRB] FUSION (O=1e5b5fbb84fa4df89c1f18e6869e1899 K=foo): trying to get from memory
[12:13:45 DBG] FUSION (O=1e5b5fbb84fa4df89c1f18e6869e1899 K=foo): memory entry not found
[12:13:45 DBG] FUSION (O=1e5b5fbb84fa4df89c1f18e6869e1899 K=foo): return DEFAULT VALUE
[12:13:45 DBG] FUSION (O=b7960e3155534d229297072ab693ceba K=foo): calling SetAsync<T> FEO[LKTO=/ DUR=1s DDUR=/ JIT=0 PR=NR FS=Y FSMAX=1m FSTHR=3s FSTO=2s FHTO=/ TOFC=Y DSTO=/ DHTO=/ ABDO=N BN=Y BBO=N]
[12:13:45 DBG] FUSION (O=b7960e3155534d229297072ab693ceba K=foo): saving entry in memory MEO[CEXP=60s PR=NR S=1] FE[FFS=N LEXP=998ms]
[12:13:45 DBG] FUSION (O=ea4eceed127a4990b7a16d6d2a7a03f3 K=foo): calling GetOrDefault<T> null
[12:13:45 VRB] FUSION (O=ea4eceed127a4990b7a16d6d2a7a03f3 K=foo): trying to get from memory
[12:13:45 DBG] FUSION (O=ea4eceed127a4990b7a16d6d2a7a03f3 K=foo): memory entry found FE[FFS=N LEXP=985ms]
[12:13:45 VRB] FUSION (O=ea4eceed127a4990b7a16d6d2a7a03f3 K=foo): using memory entry
[12:13:45 DBG] FUSION (O=ea4eceed127a4990b7a16d6d2a7a03f3 K=foo): return FE[FFS=N LEXP=981ms]
[12:13:47 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): calling GetOrSetAsync<T> FEO[LKTO=/ DUR=1s DDUR=/ JIT=0 PR=NR FS=Y FSMAX=30s FSTHR=3s FSTO=1s FHTO=/ TOFC=Y DSTO=/ DHTO=/ ABDO=N BN=Y BBO=N]
[12:13:47 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): trying to get from memory
[12:13:47 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): memory entry found (expired) FE[FFS=N LEXP=-533ms]
[12:13:47 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): waiting to acquire the LOCK
[12:13:47 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): LOCK acquired
[12:13:47 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): trying to get from memory
[12:13:47 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): memory entry found (expired) FE[FFS=N LEXP=-541ms]
[12:13:47 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): calling the factory (timeout=1s)
[12:13:48 WRN] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): a synthetic timeout occurred while calling the factory
ZiggyCreatures.Caching.Fusion.SyntheticTimeoutException: The operation has timed out.
   at ZiggyCreatures.Caching.Fusion.Internals.FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync[TResult](Func`2 asyncFunc, TimeSpan timeout, Boolean cancelIfTimeout, Action`1 timedOutTaskProcessor, CancellationToken token) in C:\Users\Jody\source\repos\ZiggyCreatures.FusionCache\src\ZiggyCreatures.FusionCache\Internals\FusionCacheExecutionUtils.cs:line 79
   at ZiggyCreatures.Caching.Fusion.FusionCache.GetOrSetEntryInternalAsync[TValue](String operationId, String key, Func`3 factory, MaybeValue`1 failSafeDefaultValue, FusionCacheEntryOptions options, CancellationToken token) in C:\Users\Jody\source\repos\ZiggyCreatures.FusionCache\src\ZiggyCreatures.FusionCache\FusionCache_Async.cs:line 191
[12:13:48 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): trying to complete the timed-out factory in the background
[12:13:48 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): trying to activate FAIL-SAFE
[12:13:48 WRN] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): FAIL-SAFE activated (from memory)
[12:13:48 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): saving entry in memory MEO[CEXP=30s PR=NR S=1] FE[FFS=Y LEXP=3s]
[12:13:48 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): releasing LOCK
[12:13:48 VRB] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): LOCK released
[12:13:48 DBG] FUSION (O=0cbf0731e56143b2b4151fe13b7faaf4 K=foo): return FE[FFS=Y LEXP=3s]
```

Normally though, this will mean a lot of storage consumed, and in a production environment usually that's not a good idea, if everything is working fine.

So we may set the min level to something like `Warning` and the output for the same operations above will look like this:

```
[12:15:41 WRN] FUSION (O=33859d31231f48a197950c3edd0cccbd K=foo): a synthetic timeout occurred while calling the factory
ZiggyCreatures.Caching.Fusion.SyntheticTimeoutException: The operation has timed out.
   at ZiggyCreatures.Caching.Fusion.Internals.FusionCacheExecutionUtils.RunAsyncFuncWithTimeoutAsync[TResult](Func`2 asyncFunc, TimeSpan timeout, Boolean cancelIfTimeout, Action`1 timedOutTaskProcessor, CancellationToken token) in C:\Users\Jody\source\repos\ZiggyCreatures.FusionCache\src\ZiggyCreatures.FusionCache\Internals\FusionCacheExecutionUtils.cs:line 79
   at ZiggyCreatures.Caching.Fusion.FusionCache.GetOrSetEntryInternalAsync[TValue](String operationId, String key, Func`3 factory, MaybeValue`1 failSafeDefaultValue, FusionCacheEntryOptions options, CancellationToken token) in C:\Users\Jody\source\repos\ZiggyCreatures.FusionCache\src\ZiggyCreatures.FusionCache\FusionCache_Async.cs:line 191
[12:15:41 WRN] FUSION (O=33859d31231f48a197950c3edd0cccbd K=foo): FAIL-SAFE activated (from memory)
```

As we can see there is way less informations produced, which in turn is less background noise for normal operations.

It's up to us to choose the desired level of logging based on our own context.
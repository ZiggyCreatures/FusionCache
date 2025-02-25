<div align="center">

![Redis logo](images/redis-logo.png)

</div>

# Redis: a couple of details

Redis has a couple of specific design and implementation details that you should be aware of:

- **multiple databases**: Redis has the concept of multiple databases. A single Redis instance can (almost, see below) completely separate data in different isolated database. This is the feature you should use if you want a single Redis instance to manage data about completely different caches, so that the same cache key can be used without collisions. An example may be 2 logical caches, one for your CMS website and one for your Consumer website, where the cache entry for the key "user/123" may contain different values and they should not confilct with one another because they are from very different logical domains (see [here](https://stackexchange.github.io/StackExchange.Redis/Configuration.html))

- **pub/sub scoping**: even though the cache entries in different databases inside the same Redis instance are completely isolated, the same cannot be said about the pub/sub messages. For whatever design decision they are received by any connected client on the same **channel** (a Redis concept to isolate different kind of messages). In theory you should specify different channel names for different logical caches sharing the same Redis instance (when you use more than one), but this is already taken care of for you thanks to the use of the `CacheName` option in the `FusionCacheOptions` object, because by default the channel name uses the cache name as a **prefix**. Having said that, if instead you are using a single Redis instance for the same deployed app twice in different environments (like dev/test), you will probably have the same cache name and, so, the same channel name. In that case you can specify a different channel prefix via the `BackplaneChannelPrefix` option in the `FusionCacheOptions` object

- **notifications sender**: in Redis, when a message is sent on a channel, all connected clients will receive it, **including the sender itself**. This would normally be a problem because setting an entry in the cache would evict that entry in all nodes including the one that just set it and that would be a waste or, even worse, a problem. But FusionCache automatically handles this by using a globally unique instance identifier (the `IFusionCache.InstanceId` property) so that the sender will automatically ignore its own notifications, without you having to do anything

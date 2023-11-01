<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🆙 Update to v0.24.0

With the [v0.24.0](https://github.com/ZiggyCreatures/FusionCache/releases/tag/v0.24.0) release, the feature known as *backplane auto-recovery* has become, simply, *auto-recovery*.

To do all of this I basically rewrote the entire part that handles distributed operations, meaning the handling of distributed cache operations and backplane operations so that they now work together in a more harmonious way, allowing the interaction between the two parts to be more in unison.

So, a couple of breaking changes were needed: in a lot of scenarios this will not affect an update to this new version, but it's better to know what will happen.

Please keep reading for more.

## ⚠ Breaking Change

First thing: if your app or service does not use the distributed cache or the backplane, the update will be totally seamless.

If instead you are using them, you need to know that the internal structure of the cache entries for the distributed cache is slightly changed: because of this the so called internal "wire format cache key modifier" that is used when pre-processing the cache key for the distributed cache, via prefix/suffix, had to be changed.

Read about "Wire Format Versioning" in the [related page](CacheLevels.md).

Moral of the story: when updating to `v0.24.0` the suggestion is to update all the apps and services that use same distributed cache.

The other main thing is that, as said, the auto-recovery feature changed from being "Backplane Auto-Recovery" to just "Auto-Recovery": this means that existing options like `EnableBackplaneAutoRecovery` is now simply `EnableAutoRecovery`, `BackplaneAutoRecoveryMaxItems` is now simply `AutoRecoveryMaxItems` and so on.

Don't worry though, all the old ones are still there and marked with the `[Obsolete]` attribute and useful instructions on what to do: so after you update just compile your project and look for warnings, is that easy.
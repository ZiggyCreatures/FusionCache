<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🆙 Update to v1.0.0

If you are updating to `v1.0` from a previous version, in general everything should be fine.

But, in some niche and specific cases, you may have to look out for some minor details or deprecations.

The 2 minor (but still technically breaking) changes are:
- slightly changed the nullability annotations with generics for the return values in the `GetOrSet<T>` and `GetOrSetAsync<T>`
- the `FusionCacheEntryOptions.Size` option went from being `long` to `long?`

Historically there have been only 2 versions that needed some attention, so please read the update notes if you are updating from a previous version:

- to update from a version before `v0.20.0` ([update notes](Update_v0_20_0.md))
- to update from a version before `v0.24.0` ([update notes](Update_v0_24_0.md))

Apart from these everything should be quite smooth.

In case something has been deprecated with time, there will be some warnings at compile time thanks to the usage of the `[Obsolete]` attribute along with useful instructions to follow, what to change, etc.

That's all, happy updating!
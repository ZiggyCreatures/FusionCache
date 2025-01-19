<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🆙 Update to v2.0.0

If you are updating from `v1.x` to `v2.0` most things should go smoothly: probably you'll just need to update the packages and rebuild, that's it.

A couple of things to keep in mind are listed below.

### 📦 Packages Update

Updating the packages means 2 things:

- update ALL the FusionCache packages, not just the main one
- update the packages in FusionCache ALL connected projects/apps

If you are using a distributed level (L2) and/or a backplane where multiple apps are connected, keep reading for the next point.

### 🗃 Wire Format Update

Something else to keep in mind is that since this is a major version update, I took the opportunity to break some things: because of this, I updated the **wire format identifier**, read more [here](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md#-wire-format-versioning) and [here](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md#-wire-format-versioning) for what this means.

Basically this means that if we have an app using `v1.4.1` and another using `v2.0.0` what happens is:
1. both apps will be able to coexist and share the same Redis instance without problems, data corruption or else
2. each app will write and read different cache entries, and an update from one app will not be seen from the other. If both apps write the same 100 entries, in Redis you'll find 200 entries (100 written for `v1.4.1` and 100 written for `v2.0.0`)

The suggestion is to try to update all the apps sharing the same resources as soon as possible to save resources.

### ⚡ Serializers

Something else to know is that now the serializers adapters are not depending on `RecyclableMemoryStreamManager` anymore, but thanks to a community member contribution are generally better nonetheless.

### 👴 Very, very old stuff

Some options/properties/methods have been marked as `[Obsolete]` for wuite a while: up until now they generated a _warning_, but with `v2` they now generate an _error_.
<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# ↩️ Auto-Recovery

Both the distributed cache and the backplane are, as the names suggest, distributed components.

This means that, as we know from the [Fallacies Of Distributed Computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing), something may go wrong while we are using them, even if only in a transient way.

For example the backplane can loose the connection for some time and each (or some) of the nodes' local memory caches will become out of sync because of some missed backplane notifications. Another example is that the distributed cache can become unavailable for a while because it is restarting or because an unhandled network topology change has disrupted the connectivity for a brief moment, and a value which has been already saved in the local memory cache may not have been saved to the distributed cache.

Looking at the available methods (like `Set`, `Remove`, `GetOrSet`, etc) we can say that the *intent* of our actions is clear, like *"I want to set the cache entry for this cache key to this value"*: wouldn't it be nice if FusionCache would help us is some way when transient error happens?

Enter **Auto-Recovery**.

With auto-recovery FusionCache will automatically detect transient errors for both the distributed cache and the backplane, and it will act accordingly to ensure that the **global state** is kept as much in-sync as possible, without any intervention on our side.

This is done thanks to an auto-recovery queue, where items are put when something bad happened during the distributed side of things: the queue is then actively processed, periodically, to ensure that as soon as possible everything will be taken care of.

More errors on a subsequent retry? Again, all taken care of until everything works out well.

This feature is not implemented **inside** of a specific backplane implementation - of which there are multiple - but inside FusionCache itself: this means that it works with any backplane implementation automatically, which is nice.

We should also keep in mind that auto-recovery works for both the distributed cache and the backplane, either when using them together or when using only one of them.

## ❤ Special Care

Sometimes the transient errors are not so transient after all, and it may happen that before a retry from the auto-recovery queue has been able to succeed a new value for the same cache key is set, on the same node.

WHat should FusionCache do?

Another nice one is when, before having been able to process an auto-recovery queue, the backplane came back on and the node received a notification for the same cache key from another node.

WHat should FusionCache do?

Special care has been put into correctly handling some common scenarios:
- if an auto-recovery item is about to be queued for a cache key for which there already is another queued item, only the last one will be kept since the result of updating the cache for the same cache key back-to-back would be the same as doing only the last one
- if a backplane notification is received on a node for a cache key for which there is a queued auto-recovery item, only the most recent one is kept: if the incoming one is newer, the local one is discarded and the incoming one is processed, otherwise the incoming one is ignored and the local one is processed to be sent to the other nodes. This avoids, for example, evicting an entry from a local cache if it has been updated after a change in a remote node, which would be useless
- when FusionCache process an item from the auto-recovery queue, it also checks if meanwhile things have changed: if the distributed cache has since been updated and the local value is not the most updated anymore, it will stop procesing the item, remove it from the queue and update the local value from the distributed one. If, on the other hand, it is still the most updated, it will proceed in updating the distributed cache and/or publish a backplane notification to update the other nodes

These and many more cases are all handled, automatically, without the need to do anything at all.

An automatic cleanup is also done to handle items in the auto-recovery queue that may have become useless: for example an item for a cache entry with a duration of `2 sec` that has been in the auto-recovery queue for more than that can be safely removed from it since it would have been already expired.

Keep reading for more "funny" scenarios, it's a ride.

## 😏 Some Examples (the easy ones)

Let's say a backplane error occurred while sending a notification: no worries, auto-recovery will automatically retry to send it as soon as possible, after the backplane will become available again.

Or maybe a value has not been saved to the distributed cache because of some temporary hiccups: again, auto-recovery will handle this automatically, by trying to save it again in the future as soon as the distributed cache will be available again.

But it's not just as simple as this: if we are using a distributed cache **and** a backplane together, a fail in saving a value to the distributed cache should also avoid sending the notification on the backplane, otherwise the other nodes may be notified of the presence of a new value, but that value has yet to be saved onto the global shared state, which is the distributed cache. By awaiting for both to be available, FusionCache makes sure that the end result is as intended.

## 😈 Another Example (a harder one)

Ok, let's up the ante a little bit.

A value is set on node `N1`: the distributed cache is updated, but the backplane fails so auto-recovery kicks in and queue the item for a later retry.

Immediately after this, and before the retry on node `N1`, a different value for the **same** cache key is set on node `N2`: here instead the distributed cache fails, so the backplane part is not even tried and, again, auto-recovery kicks in for a later retry.

Finally a node `N3` sits there, doing nothing, without having a value for that cache key in its local memory cache.

Now the distributed and the backplane both come back: at slightly different times both `N1` and `N2` will try to process their own auto-recovery queue, not knowing that other nodes may have updated the same cache entry for the same cache key, at slightly different times and with different values.

Now let's look at 2 different ways this can play out.

In the first let's say that `N1` starts processing the auto-recovery queue first, and it sees that the value in the distributed cache is still aligned with the local one, so it will proceed sending the backplane notifications to the other nodes to warn them about the update: `N3` receives the notification from `N1`, sees that it is not related to a cache entry that it has in its local memory cache and does nothing, since there's nothing to do. `N2` instead sees the notification from `N1` and notices that it has a queued one which is newer: because of this it will discard the incoming notification, and proceed by processing the pending one. In doing this it sees that the value in the distributed cache is older, and so it updates the distribted cache and sends the notification to the other nodes (`N1` and `N3`). `N3` again sees the incoming notification and does nothing, for the same reason as before. `N1` instead receives the notification from `N2`, checks its local memory cache, sees that it's older and it updates the local value from the distributed cache.

In the second let's say that `N2` starts processing the auto-recovery queue first, and it sees that the value in the distributed cache is older than the local one so it will proceed updating the distributed cache and, if that succeeds, will also send the backplane notifications to the other nodes to warn them about the update: `N3` receives the notification from `N2`, sees that it is not related to a cache entry that it has in its local memory cache and does nothing, since there's nothing to do. `N1` instead sees the notification from `N2` and notices that it has a queued one which is older: because of this it will discard the queued one and update the local memory cache with the distributed one.

In both cases now everything is perfectly aligned.

## 🐲 Hic Sunt Dracones (the epic one)

Let's say the situation is the same as the previous one, but the nodes are 10, from `N1` to `N10`.

Now let's say that 6 of them had updates on them and, of those 6, 3 failed with the distributed cache and the other 3 succeeded with the distributed cache but failed with the backplane.

Now the distributed cache comes back, but not the backplane: the processing of the auto-recovery queue starts on each node, and each node updates the distributed cache if they see that their version is the most updated one or update their local copy in case is the opposite, but since the backplane is still down they will all fail to complete the auto-recovery process.

After some time the distributed cache goes down again, but - surprise - the backplane comes back up.

Again the auto-recovery queue processing starts but, again, with no luck.

While all of this happens and while there are all these queued auto-recovery items to still process, here come some new updates for the same cache key on a couple of nodes, let's say on nodes `N4` and `N6`: in their own local auto-recovery queues the items for that cache key will be replaced by the new ones automatically.

Then both the distributed cache and the backplane come back up, this time together.

And now the magic happens, and all the pending checks on the distributed cache and all the pending backplane notifications will be published, an all the conflicts will be resolved based on which piece of data is more fresh and both the data in the distributed cache and all the nodes that have data for that cache key (and only those) will be updated with the latest version.

And now all the nodes that had a cached value for that cache key are all aligned.

And the out-of-sync dragon has been defeated, and all of the nodes lived happily ever after.

*Fin*.

## 🖥️ Seeing is believing

Wanna see auto-recovery in action?

Sure, why not? Thanks to the [Simulator](Simulator.md) it's very easy:

[![FusionCache Simulator](https://img.youtube.com/vi/6jGX6ePgD3Q/maxresdefault.jpg)](https://youtu.be/6jGX6ePgD3Q)
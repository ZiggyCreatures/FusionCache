<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# 🖥️ Simulator

In general it is quite complicated to understand what is going on in a distributed system.

When using FusionCache with the [distributed cache](CacheLevels.md) and the [backplane](Backplane.md), a lot of stuff is going on at any given time: add to that intermittent transient errors and, even if we can be confident [auto-recovery](AutoRecovery.md) will handle it all automatically, clearly **seeing** the whole picture can become a daunting task.

It would be very useful to have *something* that let us clearly *see* it all in action, something that would let us configure different components, tweak some options, enable this, disable that and let us *simulate* a realistic workload to see the results.

Luckily there is, and is called **Simulator**.

Here's a video showcasing it (and some auto-recovery, too):

[![FusionCache Simulator](https://img.youtube.com/vi/redH-2qs-gk/maxresdefault.jpg)](https://youtu.be/redH-2qs-gk)

## 🛝 Play With It

Of course, we can just clone the repo and play with it ourselves!

After getting the FusionCache source code by cloning the repo we can look for a project called, well, Simulator and run it: and here we go simulating away all the crazy scenarios we can think of.

A varied sequence of updates on a multi-cluster and multi-node situation? Sure, why not!

Remove the distributed cache and just use the backplane? Yep, that's easy.

Memory only, with no distributed cache and no backplane? Up to you.

Simulate some broken distributed cache mixed with a flickering backplane and some high frequency random updates to see if auto-recovery holds up? No problem.

## 👩‍🏫 How It Works

After launching the project we just have to specify what we want to simulate: how many clusters, how many nodes per each cluster and if we want [fail-safe](FailSafe.md) enabled or not.

Then the simulated clusters will be created and, for each cluster, the nodes will also be created, then a simulated distributed cache (if you want) and a simulated backplane (again, if you want) per each cluster, so that they'll be used by all the nodes of each cluster to share data and talk to each others.

A nice dashboard will then be shown where in general the colors mean:
- 🟩 GREEN: it means the node/cluster is **synchronized**
- 🟥 RED: it means the node/cluster is **out of sync**

Finally we'll have some shortcuts available to us to do stuff, like:
- `0`: enable/disable periodic updates on a random node on a random cluster
- `1-N`: update a random node on the cluster of your choosing, up to N (the number of cluster you specified)
- `D/d`: enable/disable the simulated distributed cache (to simulate a transient failure)
- `B/b`: enable/disable the simulated backplane (to simulate a transient failure)
- `S/s`: enable/disable the simulated database (to simulate a transient failure)
- `Q/q`: quit

## ⭐ Credit Where Credit Is Due
One final thing: the Simulator is built using the wonderful [Spectre.Console](https://spectreconsole.net/) library by Patrik Svensson, Phil Scott, Nils Andresen and other contributors: please take a look at the [repo](https://github.com/spectreconsole/spectre.console) and give it a star!
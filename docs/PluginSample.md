<div align="center">

![FusionCache logo](logo-plugin-128x128.png)

</div>

# :jigsaw: A plugin sample

Let's say we want to create a [plugin](Plugins.md) that sends an email when a fail-safe activation happens.

For this example we will use the awesome [MailKit](https://github.com/jstedfast/MailKit) and [MimeKit](https://github.com/jstedfast/MimeKit) libraries by the great [Jeffrey Stedfast](https://github.com/jstedfast) and the free [Ethereal](https://ethereal.email/) fake smtp service.

| 🚨 WARNING |
|:-------|
| Please keep in mind this is just an example: **don't send emails this way in real world projects!** |


## Getting started

We'll start by creating the plugin itself, which would listen to **fail-safe activation** events

```csharp
using System;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Plugins.MyAwesomePlugins
{
  public class FailSafeEMailPlugin
    : IFusionCachePlugin
  {

    public void Start(IFusionCache cache)
    {
      // ADD THE HANDLER
      cache.Events.FailSafeActivate += OnFailSafeActivate;
    }

    public void Stop(IFusionCache cache)
    {
      // REMOVE THE HANDLER
      cache.Events.FailSafeActivate -= OnFailSafeActivate;
    }

    private void OnFailSafeActivate(object sender, FusionCacheEntryEventArgs e)
    {
      // DO SOMETHING HERE...
    }
  }
}
```

As you can see we register an event handler in the `Start` method and, the keep things clean, we remove it in the `Stop` method.

Then we'll add the specific code to send emails:

```csharp
private void OnFailSafeActivate(object sender, FusionCacheEntryEventArgs e)
{
  // PREPARE THE MAIL MESSAGE
  var email = new MimeMessage();
  email.From.Add(MailboxAddress.Parse("[FROM_ADDRESS]"));
  email.To.Add(MailboxAddress.Parse("[TO_ADDRESS]"));
  email.Subject = "Fail-safe has been activated";
  email.Body = new TextPart(TextFormat.Plain)
  {
    Text = $"A fail-safe activation has occurred at {DateTimeOffset.UtcNow:O} UTC for the cache key {e.Key}"
  };

  // SEND IT
  using (var smtp = new SmtpClient())
  {
    smtp.Connect("smtp.ethereal.email", 587, SecureSocketOptions.StartTls);
    smtp.Authenticate("[USERNAME]", "[PASSWORD]");
    smtp.Send(email);
    smtp.Disconnect(true);
  }
}
```

Anything bad that may happen in the handler itself will be automatically logged by FusionCache - of course if a logger has been registered - so there's no need to do anything extra about it.


## Usage

When using DI we can then simply register it to be used in the ConfigureServices method in our classic Startup.cs file (or wherever we wire up our DI container), like this:

```csharp
services.AddSingleton<IFusionCachePlugin, FailSafeEMailPlugin>();
```

and FusionCache will pick it up automatically.

If instead we decide to go **without DI**, we simply have to do it this way:

```csharp
var myPlugin = new FailSafeEMailPlugin();

var cache = new FusionCache(new FusionCacheOptions());

cache.AddPlugin(myPlugin);
```


## Options

All of this is fine, but it would really be better to avoid hard-coding the email **From** and **To** addresses, the **SMTP host** and so on in the source code, right?

Being good .NET citizens, we can add some options to our plugin by following the [Options pattern](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options) as is standard practice.

So we'll create a specific options class (that also implements the `IOptions<T>` interface for better ease of use):

```csharp
public class FailSafeEMailPluginOptions
  : IOptions<FailSafeEMailPluginOptions>
{
  public string? FromAddress { get; set; }
  public string? ToAddress { get; set; }
  public string? SmtpHost { get; set; }
  public int SmtpPort { get; set; } = 25;
  public string? SmtpUsername { get; set; }
  public string? SmtpPassword { get; set; }

  FailSafeEMailPluginOptions IOptions<FailSafeEMailPluginOptions>.Value
  {
    get { return this; }
  }
}
```

Then we'll use it in our plugin:

```csharp
public class FailSafeEMailPlugin
  : IFusionCachePlugin
{

  private FailSafeEMailPluginOptions _options;

  public FailSafeEMailPlugin(IOptions<FailSafeEMailPluginOptions> optionsAccessor)
  {
    // GET THE OPTIONS
    _options = optionsAccessor.Value;
  }

  public void Start(IFusionCache cache)
  {
    // ADD THE HANDLER
    cache.Events.FailSafeActivate += OnFailSafeActivate;
  }

  public void Stop(IFusionCache cache)
  {
    // REMOVE THE HANDLER
    cache.Events.FailSafeActivate -= OnFailSafeActivate;
  }

  private void OnFailSafeActivate(object sender, FusionCacheEntryEventArgs e)
  {
    // PREPARE THE MAIL MESSAGE
    var email = new MimeMessage();
    email.From.Add(MailboxAddress.Parse(_options.FromAddress));
    email.To.Add(MailboxAddress.Parse(_options.ToAddress));
    email.Subject = "Fail-safe has been activated";
    email.Body = new TextPart(TextFormat.Plain)
    {
      Text = $"A fail-safe activation has occurred at {DateTimeOffset.UtcNow:O} UTC for the cache key {e.Key}"
    };

    // SEND IT
    using (var smtp = new SmtpClient())
    {
      smtp.Connect(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls);
      smtp.Authenticate(_options.SmtpUsername, _options.SmtpPassword);
      smtp.Send(email);
      smtp.Disconnect(true);
    }
  }
}
```

Now we can imagine going on and let our users configure the plugin even more, maybe by passing a generic *mail sending service* instead of the SMTP specific one or really anything else we can think of, but I'll leave this as an exercise.

One last thing it would be nice to have is a custom extension method for registering the plugin in the DI container, to allow for the plugin options to be configured in a strongly typed way.

So we'll add a file like this (notice the containing namespace, per Microsoft best practices):

```csharp
using System;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Plugins.MyAwesomePlugins;

namespace Microsoft.Extensions.DependencyInjection
{
  public static class FailSafeEMailPluginServiceCollectionExtensions
  {
    public static IServiceCollection AddFusionCacheFailSafeEMailPlugin(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null)
    {
      if (services is null)
        throw new ArgumentNullException(nameof(services));

      // ENSURE THE OPTIONS SYSTEM IS AVAILABLE
      services.AddOptions();

      // OPTIONAL CUSTOM CONFIGURATION OF OPTIONS
      if (setupOptionsAction is object)
        services.Configure(setupOptionsAction);

      //REGISTER THE SERVICE
      services.AddSingleton<IFusionCachePlugin, FailSafeEMailPlugin>();

      return services;
    }
  }
}
```

By doing this we are allowing our users to have a nice, strongly typed way of registering and configuring our plugin, like this:

```csharp
services.AddFusionCacheFailSafeEMailPlugin(options =>
{
  options.FromAddress = "sender@example.org";
  options.ToAddress = "target@example.org";
  options.SmtpHost = "smtp.ethereal.email";
  options.SmtpPort = 587;
  options.SmtpUsername = "[USERNAME]";
  options.SmtpPassword = "[PASSWORD]";
});
```

Of course all of this can still be done even **without DI** at all:

```csharp
var myPlugin = new FailSafeEMailPlugin(new FailSafeEMailPluginOptions()
{
  FromAddress = "sender@example.org",
  ToAddress = "target@example.org",
  SmtpHost = "smtp.ethereal.email",
  SmtpPort = 587,
  SmtpUsername = "[USERNAME]",
  SmtpPassword = "[PASSWORD]"
});

var cache = new FusionCache(new FusionCacheOptions());

cache.AddPlugin(myPlugin);
```

## End result

This is what the finished plugin would look like, splitted in 3 different files.

File `FailSafeEMailPluginOptions.cs`:

```csharp
using System;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Plugins.MyAwesomePlugins
{
  public class FailSafeEMailPluginOptions
    : IOptions<FailSafeEMailPluginOptions>
  {
    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }

    FailSafeEMailPluginOptions IOptions<FailSafeEMailPluginOptions>.Value
    {
      get { return this; }
    }
  }
}
```

File `FailSafeEMailPlugin.cs`:

```csharp
using System;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using ZiggyCreatures.Caching.Fusion.Events;

namespace ZiggyCreatures.Caching.Fusion.Plugins.MyAwesomePlugins
{
  public class FailSafeEMailPlugin
    : IFusionCachePlugin
  {

    private FailSafeEMailPluginOptions _options;

    public FailSafeEMailPlugin(IOptions<FailSafeEMailPluginOptions> optionsAccessor)
    {
      // GET THE OPTIONS
      _options = optionsAccessor.Value;
    }

    public void Start(IFusionCache cache)
    {
      // ADD THE HANDLER
      cache.Events.FailSafeActivate += OnFailSafeActivate;
    }

    public void Stop(IFusionCache cache)
    {
      // REMOVE THE HANDLER
      cache.Events.FailSafeActivate -= OnFailSafeActivate;
    }

    private void OnFailSafeActivate(object sender, FusionCacheEntryEventArgs e)
    {
      // PREPARE THE MAIL MESSAGE
      var email = new MimeMessage();
      email.From.Add(MailboxAddress.Parse(_options.FromAddress));
      email.To.Add(MailboxAddress.Parse(_options.ToAddress));
      email.Subject = "Fail-safe has been activated";
      email.Body = new TextPart(TextFormat.Plain)
      {
        Text = $"A fail-safe activation has occurred at {DateTimeOffset.UtcNow:O} UTC for the cache key {e.Key}"
      };

      // SEND IT
      using (var smtp = new SmtpClient())
      {
        smtp.Connect(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls);
        smtp.Authenticate(_options.SmtpUsername, _options.SmtpPassword);
        smtp.Send(email);
        smtp.Disconnect(true);
      }
    }
  }
}
```

File `FailSafeEMailPluginServiceCollectionExtensions.cs`:

```csharp
using System;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Plugins.MyAwesomePlugins;

namespace Microsoft.Extensions.DependencyInjection
{
  public static class FailSafeEMailPluginServiceCollectionExtensions
  {
    public static IServiceCollection AddFusionCacheFailSafeEMailPlugin(this IServiceCollection services, Action<FusionCacheOptions>? setupOptionsAction = null)
    {
      if (services is null)
        throw new ArgumentNullException(nameof(services));

      // ENSURE THE OPTIONS SYSTEM IS AVAILABLE
      services.AddOptions();

      // OPTIONAL CUSTOM CONFIGURATION OF OPTIONS
      if (setupOptionsAction is object)
        services.Configure(setupOptionsAction);

      //REGISTER THE SERVICE
      services.AddSingleton<IFusionCachePlugin, FailSafeEMailPlugin>();

      return services;
    }
  }
}
```

<br/>
<br/>
<br/>
<br/>

## 💅 Final polishing

Just a couple of suggestions here.


### Name

Choose a name that clearly explain what the plugin does: I know, naming things is hard, but take your time.


### Namespace

You can of course use the namespace you prefer, but if you want to be in the common FusionCache namespace I'd suggest going with something like `ZiggyCreatures.Caching.Fusion.Plugins.BrandOrCompanyOrFeatureSubject.MyPlugin` to keep everything in place and make it such that any other potential plugin will not collide with others.


### Xml Comments

It would be nice for users of our plugin to know what they are doing **while** they are doing it. By using [xml comments](https://docs.microsoft.com/en-us/dotnet/csharp/codedoc) you can ensure a nice learning path so that each property, method or option available is clearly documented and they will learn while doing it.


### Documentation

If the features available are a lot and/or complicated, it would useful for users to have some help pages to go to for a deeper learning on top of code comments.

A couple of simple markdown pages hosted directly on the GitHub repo page or wherever you want would do the trick.


### Package Name

Nuget package naming works via prefix so, for example, official Microsoft stuff is named `Microsoft.*` and it is possible to "lock" prefixes via a reservation process: because of this, the `ZiggyCreatures.*` prefix in Nuget is reserved and will be used for other ZiggyCreatures projects in the future.

One thing you can do if you want to be found on Nuget when searching for FusionCache packages would be to put `ZiggyCreatures.FusionCache` in the name.

An example may be something like:

- `JaneDoe.ZiggyCreatures.FusionCache.Plugins.Metrics.OpenTelemetryPlugin`
- `JonDoe.ZiggyCreatures.FusionCache.Plugins.Notifications.MyNotificationsPlugin`

Obviously the package - if you want to create one - in the end is all yours so these are just suggestions.

Oh, and if you come up with a better way please [**:envelope: drop me a line**](https://twitter.com/jodydonetti)!


### Package Logo

Of course use your own logo if you like, it goes without saying!

Please **⚠ DON'T USE** the official FusionCache logo as-is for your own third-party plugin or other packages: the reasoning is I'd like for users to differentiate between **official packages** and **third party** packages.

A lot of times though it's not easy to create or came up with a nice logo, and the default one in Nuget would not make your package emerge.

So, to keep a common branding and make FusionCache plugins recognizable I've created a slightly different logo, available in different sizes:

**128x128:**

![FusionCache logo - 400x400](logo-plugin-128x128.png)

**256x256:**

![FusionCache logo - 400x400](logo-plugin-256x256.png)

**400x400:**

![FusionCache logo - 400x400](logo-plugin-400x400.png)

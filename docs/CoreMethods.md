<div align="center">

![FusionCache logo](logo-128x128.png)

</div>

# :joystick: Core Methods

At a high level there are 5 core methods:

- `Set[Async]`
- `Remove[Async]`
- `TryGet[Async]`
- `GetOrDefault[Async]`
- `GetOrSet[Async]`

All of them work **on both the memory cache and the distributed cache** (if any) in a transparent way: you don't have to do anything extra for it to coordinate the 2 layers.

All of them are available in both a **sync** and an **async** version.

Finally, most of them have a set of **overloads** for a better ease of use.

If you are thinking *"which one should I use?"* please keep reading.


## Set[Async]

It is used to **SET** a value in the cache for the specified key using the specified options. If something is already there, it will be overwritten.

Examples:

```csharp
// DEFAULT OPTIONS
cache.Set("foo", 42);

// SPECIFIC DURATION
cache.Set("foo", 42, TimeSpan.FromSeconds(30));
```

## Remove[Async]

It is used to **REMOVE** a value from the cache for the specified key. If nothing is there, nothing will happen.

Examples:

```csharp
cache.Set("foo", 42);

// THIS WILL REMOVE THE CACHE ENTRY FOR "foo"
cache.Remove("foo");

// THIS WILL DO NOTHING
cache.Remove("foo");
```

## GetOrDefault[Async]

It is used to **GET** the value in the cache for the specified key and, if nothing is there, returns a **DEFAULT VALUE**.

With this method **NOTHING IS SET** in the cache.

It is useful if you want to use what's in the cache or some default value just for this call, but **you don't want to change the state** of the cache itself.

Examples:

```csharp
// THIS WILL GET BACK 42
foo = cache.GetOrDefault("foo", 42);

// IF WE IMMEDIATELY CALL THIS, WE WILL GET BACK 21
foo = cache.GetOrDefault("foo", 21);

// THIS WILL GET BACK 0, WHICH IS THE DEFAULT VALUE FOR THE TYPE int
foo = cache.GetOrDefault<int>("foo");

// ALSO USEFUL FOR USER PREFERENCES: WE CAN USE A DEFAULT VALUE WITHOUT SETTING ONE
var enableUnicorns = cache.GetOrDefault<bool>("flags.unicorns", false);

// AND SINCE false IS THE DEFAULT VALUE FOR THE TYPE bool WE CAN SIMPLY DO THIS
var enableUnicorns = cache.GetOrDefault<bool>("flags.unicorns");
```

## TryGet[Async]

It is used to **CHECK** if a value is in the cache for the specified key and, if so, to **GET** the value itself all at once.

With this method **NOTHING IS SET** in the cache.

You may be wondering what is the difference between this and the previous `GetOrDefault` method: think about what happens if you do a `cache.GetOrDefault<int>("foo")` and get back `0`. Does it mean that `0` was in the cache or that it was not, and the default value for `int` has been returned?

If you don't care about this difference you can just use `GetOrDefault`, but if you really want to know if something was there you can call `TryGet` instead and avoid any confusion.

The return value is of type `MaybeValue<T>` which is a type similar to the standard `Nullable<T>` but for both reference and value types (see below for more).

Examples:

```csharp
var maybeFoo = cache.TryGet<int>("foo");

if (maybeFoo.HasValue) {
    // SUCCESS: THE VALUE WAS THERE

    // GET THE VALUE
    var result = maybeFoo.Value;
} else {
    // FAIL: THE VALUE WAS NOT THERE
}

// DOING THIS WITHOUT CHECKING MAY THROW AN InvalidOperationException IF THE VALUE WAS NOT THERE
var result = maybeFoo.Value;

// THIS WILL GET THE VALUE, IF IT WAS THERE, OR THE SPECIFIED DEFAULT VALUE OTHERWISE
var result = maybeFoo.GetValueOrDefault(42);

// THIS WILL GET THE VALUE, IF IT WAS THERE, OR THE DEFAULT VALUE OF int OTHERWISE
var result = maybeFoo.GetValueOrDefault();

// YOU CAN ALSO USE AN IMPLICIT CONVERSION BETWEEN MaybeValue<T> AND T
// BUT REMEMBER, IF NO VALUE IS THERE IT THROWS AN InvalidOperationException
int result = maybeFoo;
```

💡 It's not possible to use the classic method signature of `bool TryGet<TValue>(string key, out TValue value)` to set a value with an `out` parameter because .NET does not allow it on async methods (for good reasons) and I wanted to keep the same signature for every method in both sync/async versions.

## GetOrSet[Async]

This is the most important and powerful method available, and it does **a lot** for you.

It tries to **GET** the value in the cache for the specified key and, if there (and not expired), it returns it.

Easy peasy.

If instead the value is not in the cache (or is expired) it can automatically **SET** a value in the cache, obtained in different ways, based on what you passed to the method after the `key` param:

- a **DEFAULT VALUE**: that will be **SET** in the cache and returned to you

- a [**FACTORY**](FactoryOptimization.md) method: it will be executed, and if the execution goes well the obtained value will be **SET** in the cache and returned to you

These are the happy paths.

But in the real world the factory execution can go **WRONG** for various reasons, like if an exception is thrown or you've specified a [timeout](Timeouts.md) and it actually occurs.

Now, if you have **NOT** enabled [**FAIL-SAFE**](FailSafe.md), an **EXCEPTION** will be thrown for you to handle.

If instead you have enabled **FAIL-SAFE**, then:

- if there's an **EXPIRED** (stale) value: it will be **SET** in the cache with the `FailSafeThrottleDuration` and returned to you
- if there is not an **EXPIRED** (stale) value:
  - if you specified a **FAIL-SAFE DEFAULT VALUE**: it will be **SET** in the cache with the `FailSafeThrottleDuration` and returned to you
  - if you have not specified a **FAIL-SAFE DEFAULT VALUE**: an **EXCEPTION** will be thrown for you to handle

Basically if you want to go the bulletproof way and be sure that, no matter what happens, you will always get a value back and never get an exception thrown in your face, you should call `GetOrSet` and specify either a **default value** or a **factory + fail-safe default value**.

Examples:

```csharp
// THIS WILL GET FOO FROM THE CACHE OR, IF NOT THERE, SET THE VALUE 123 AND RETURN IT
var foo = cache.GetOrSet<int>("foo", 123);

// THIS WILL GET FOO FROM THE CACHE OR CALL THE FACTORY, THEN SET THE VALUE OBTAINED AND RETURN IT
var foo = cache.GetOrSet<int>(
    "foo",
    _ => GetFooFromDb()
);

// AS ABOVE, BUT WITH AN EXPLICIT DURATION OF 1 MIN
var foo = cache.GetOrSet<int>(
    "foo",
    _ => GetFooFromDb()
    TimeSpan.FromMinutes(1)
);

// OR 
var foo = cache.GetOrSet<int>(
    "foo",
    _ => GetFooFromDb()
    options => options.SetDuration(TimeSpan.FromMinutes(1))
);

// AS ABOVE, BUT IF BAD STUFF HAPPENS, WILL USE EXPIRED (STALE) DATA INSTEAD OF THROWING AN EXCEPTION
// ALSO:
// 1) STALE DATA WILL BE USED FOR 30 SEC AT A TIME, FOR A MAXIMUM OF 1 HOUR
// 2) IF THERE IS NO STALE DATA TO USE, AN EXCEPTION WILL BE THROWN NONETHELESS
var foo = cache.GetOrSet<int>(
    "foo",
    _ => GetFooFromDb()
    options => options
        .SetDuration(TimeSpan.FromMinutes(1))
        .SetFailSafe(true, TimeSpan.FromHours(1), TimeSpan.FromSeconds(30))
);

// AS ABOVE, BUT IF THERE IS NO STALE DATA, A DEFAULT VALUE WILL BE SET (FOR 30 SEC, ETC...) ADN RETURNED
var foo = cache.GetOrSet<int>(
    "foo",
    _ => GetFooFromDb()
    42
    options => options
        .SetDuration(TimeSpan.FromMinutes(1))
        .SetFailSafe(true, TimeSpan.FromHours(1), TimeSpan.FromSeconds(30))
);

// OR, IF YOUR DefaultEntryOptions ALREADY HAVE FAIL-SAFE ENABLED AND THE
// RIGHT OPTIONS, YOU CAN JUST DO THIS
var foo = cache.GetOrSet<int>("foo", _ => GetFooFromDb(), 42);
```

## :recycle: Common overloads

Every core method that needs a set of options (`FusionCacheEntryOptions`) for how to behave has different overloads to let you specify these options, for better ease of use.

You can choose between passing:

- **Nothing**: you don't pass anything, so the global `DefaultEntryOptions` will be used (also saves some memory allocations)
- **Options**: you directly pass a `FusionCacheEntryOptions` object. This gives you total control over each option, but you have to instantiate it yourself and **does not copy** the global `DefaultEntryOptions`
- **Setup action**: you pass a `lambda` that receives a duplicate of the `DefaultEntryOptions` so you start from there and modify it as you like (there's also a set of *fluent methods* to do that easily)
- **Duration**: you simply pass a `TimeSpan` value for the duration. This is the same as the previous one (start from the global default + lambda) but for the common scenario of when you only want to change the duration

## 🤷‍♂️ `MaybeValue<T>`
The special type `MaybeValue<T>` is similar to the standard `Nullable<T>` type in .NET, but usable for both value types and reference types.

The type has a `bool HasValue` property to know if it contains a value or not (like standard nullables), and a `T Value` property with the value itself: if `HasValue` is false, trying to access the `Value` property would throw an `InvalidOperationException`, again like standard nullables.

The type also is implicitly convertible to and from values of type `T`, so that you don't have to manually do it yourself.

Finally, if you want to get the value inside of it or a default one if case it's not there, you can use the `GetValueOrDefault()` method, with or without the default value to use (again, just like standard nullables.

Currently this type is used inside FusionCache in 2 places:

- as the return type of the `TryGet[Async]` methods, as a way to communicate both the presence of the value and, if so, the value itself
- as the type for the `failSafeDefaultValue` param in the `GetOrSet[Async]` methods, so that it's possible to clearly specify with only 1 param both "no value" or the value itself, easily


Oh, and if you are into *functional programming* you may smell a scent of Option/Maybe monad: yep, can confirm 👍


## 🤔 Why no `Get<T>` ?
You may be wondering why the quite common `Get<T>` method is missing.

It is because its behaviour would correspond to FusionCache's `GetOrDefault<T>` method above, but with 2 problems:

1) the name is not explicit about what happens when no data is in the cache: will it return some default value? Will it throw an exception? Taking a hint from .NET's `Nullable<T>` type (like `Nullable<int>` or `int?`), it is better to be explicit, so the `GetOrDefault` name has been preferred

2) having only a `Get<T>` method would make it impossible to determine if something is in the cache or not. If for example we would do something like `cache.Get<Product>("foo")` and it returns `null`, does it mean that nothing was in the cache (so better go check the database) or that `null` was in the cache (so we already checked the database, the product was not there, and we should not check the database again for some time)?

By being explicit and having 2 methods (`GetOrDefault<T>` and `TryGet<T>`) we remove any doubt a developer may have and solve the above issues.
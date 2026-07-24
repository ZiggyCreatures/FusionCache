# Azure Service Bus Backplane Implementation Plan

## Goal

Bring `ZiggyCreatures.FusionCache.Backplane.AzureServiceBus` to release-ready quality: a correct broadcast topology, reliable lifecycle handling, supported authentication modes, an executable test suite, and documented deployment guidance.

## Delivery order

1. Stabilize the internal contract and tests.
2. Define the subscription topology and ownership rules.
3. Implement authentication, validation, and client construction.
4. Complete provisioning, filtering, and cleanup.
5. Harden runtime behavior and add broker-backed coverage.
6. Finish documentation and package verification.

## 1. Stabilize the internal contract and test suite

- Finalize the wrapper contract around:
  - `AzureServiceBusClientWrapper`
  - `AzureServiceBusAdminWrapper`
  - `IAsyncDisposable.DisposeAsync`
- Update Azure Service Bus unit tests, integration tests, shared test helpers, and L1/L2 backplane tests to use that contract.
- Add `DisposeAsync` implementations to all fake wrappers.
- Remove stale references to the deleted API, including `AzureServiceBusNaming`, `AzureServiceBusAdminProvisioner`, `UnprovisionAsync`, and old wrapper constructors.
- Do not proceed until the Azure Service Bus test subset compiles and passes.

**Acceptance criteria**

- `dotnet test tests/ZiggyCreatures.FusionCache.Tests/ZiggyCreatures.FusionCache.Tests.csproj --filter "FullyQualifiedName~AzureServiceBus"` compiles and passes.
- Tests exercise the current production types rather than compatibility shims.

## 2. Define subscription topology and ownership

Azure Service Bus topics broadcast only when every cache node consumes through its own subscription. Multiple nodes sharing a subscription compete for messages and will miss invalidations.

- In admin mode, generate one unique subscription per cache-process instance by default.
- In non-admin mode, require an externally provisioned, unique subscription per cache-process instance.
- Define ownership rules for manually supplied subscription names in admin mode:
  - whether the library may create it;
  - whether the library may delete it;
  - how this differs from externally provisioned resources.
- Add an explicit instance/subscription identity option if the current `SubscriptionName` property is insufficient to describe the deployment model.
- Document required Azure permissions for each mode.

**Acceptance criteria**

- A multi-node integration test proves one published invalidation reaches every other node.
- Configuration and documentation make a shared subscription an explicitly unsupported multi-node topology.

## 3. Implement options validation and client creation

- Add one validation path, used before a backplane is constructed.
- Support exactly one authentication mode:
  - connection string; or
  - fully qualified namespace plus `TokenCredential`.
- Reject missing, partial, or conflicting configurations with actionable exceptions.
- Construct both `ServiceBusClient` and `ServiceBusAdministrationClient` from the selected authentication mode.
- Consider optional client factories for advanced hosts and deterministic tests; define ownership so caller-provided clients are not disposed by the backplane.
- Apply `LockTimeout` consistently to all locks; remove hard-coded lock timeouts.

**Acceptance criteria**

- Unit tests cover connection-string authentication, token-credential authentication, invalid configurations, and client-factory precedence if factories are added.
- Identity-only configuration works without requiring a connection string.

## 4. Align default topic selection with FusionCache channels

- Decide and document whether the default topic derives from `BackplaneSubscriptionOptions.ChannelName` or `CacheName`.
- Prefer `ChannelName` when protocol/version isolation is expected from FusionCache's normal channel naming.
- Keep `TopicName` as an explicit override.
- Retain deterministic sanitization, length limits, and valid fallbacks.
- Add tests for invalid characters, long names, fallback names, and topic isolation.

**Acceptance criteria**

- The value validated during subscription is also the value used to derive the default topic.
- Two incompatible channels cannot silently share the same default topic.

## 5. Complete provisioning and self-message filtering

- Create subscriptions using `SubscriptionAutoDeleteOnIdle`.
- Add a server-side rule that excludes messages whose `ConnectionId` matches the local subscription identity.
- Remove or replace the default match-all rule so the exclusion rule takes effect.
- Make topic, subscription, and rule creation idempotent and safe against concurrent starts.
- Define behavior for existing subscriptions and rules in admin mode: validate/repair them or fail with a clear diagnostic.
- Keep the in-process self-message guard as defense in depth, not as the primary filtering mechanism.

**Acceptance criteria**

- Real-broker tests show self-published messages are filtered server-side.
- The configured auto-delete interval is observable on the created subscription.
- Concurrent startup does not fail due to benign `AlreadyExists` races.

## 6. Implement lifecycle ownership and cleanup

- Make the backplane explicitly own shutdown, preferably with `IAsyncDisposable`.
- On unsubscribe/dispose:
  - detach `SubscriptionMissing` handlers;
  - stop and dispose the processor;
  - dispose the sender and library-created Service Bus client;
  - delete only subscriptions the instance created and owns;
  - never delete externally provisioned non-admin resources.
- Make cleanup idempotent and preserve the primary operation exception when cleanup also fails.
- Clear local state after successful teardown so an intentional future subscribe can start cleanly, if re-subscription is supported.

**Acceptance criteria**

- Repeated unsubscribe/dispose calls are safe.
- Disposal stops the processor and releases owned SDK resources.
- Non-admin disposal never attempts an administrative operation.

## 7. Harden subscribe, recovery, and message processing

- Make duplicate `SubscribeAsync` calls either fail clearly or be fully idempotent; choose one behavior and test it.
- Roll back state and event registration if provisioning, processor startup, or connect callbacks fail.
- Serialize subscription recovery after `MessagingEntityNotFound` to prevent repeated provisioning attempts.
- Re-establish processing after recovery and invoke the FusionCache connection handler with `IsReconnection = true`.
- Snapshot message handlers before invocation to avoid concurrent mutation while dispatching.
- Define and test handling for malformed bodies, missing properties, handler failures, abandon/retry, and dead-letter behavior.
- Deliberately configure processor concurrency and prefetch behavior; expose options only where necessary.

**Acceptance criteria**

- A deleted auto-delete subscription is recreated and resumes processing.
- Failure during subscribe leaves no orphan event handler or partial state.
- Invalid messages have deliberate, tested settlement behavior.

## 8. Documentation and packaging

- Add a package README and include it in the project file.
- Document:
  - connection-string and managed-identity setup;
  - admin versus non-admin permissions;
  - per-instance subscription/IaC requirements;
  - topic and subscription naming;
  - cleanup and auto-delete behavior;
  - Azure Service Bus emulator integration tests.
- Include a minimal multi-node configuration example.
- Verify package icon, README, dependencies, and target frameworks during packing.

**Acceptance criteria**

- `dotnet pack` creates an installable package with its README included.
- A user can configure both supported authentication modes by following the package README alone.

## 9. Final verification

- Run all unit tests across supported target frameworks.
- Run emulator or real-broker integration tests in CI.
- Add coverage for:
  - multi-node broadcast delivery;
  - identity authentication;
  - non-admin externally provisioned subscriptions;
  - reconnect and subscription recreation;
  - self-message filtering;
  - duplicate subscribe and repeated disposal.
- Run `dotnet test`, `dotnet pack`, formatting/analyzers, and a package-consumption smoke test before merge.

## Priority

The required order is:

1. Test-contract repair.
2. Subscription topology decision.
3. Authentication and validation.
4. Provisioning and lifecycle implementation.
5. Integration coverage and documentation.

This order prevents the project from shipping a configuration that appears valid but either cannot authenticate, leaks resources, or fails to deliver cache invalidations to every node.

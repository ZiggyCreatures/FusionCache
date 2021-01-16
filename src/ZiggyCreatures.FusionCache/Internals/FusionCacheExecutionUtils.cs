using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	/// <summary>
	/// A set of utility methods to deal with sync/async execution of actions/functions, with support for timeouts, fire-and-forget execution, etc.
	/// </summary>
	public static class FusionCacheExecutionUtils
	{
		private static readonly TaskFactory _taskFactory = new TaskFactory(
			CancellationToken.None,
			TaskCreationOptions.None,
			TaskContinuationOptions.None,
			TaskScheduler.Default
		);

		/// <summary>
		/// Run an async function asynchronously with a timeout and some additional ooptions.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="asyncFunc">The async function to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out function.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The resulting <see cref="Task"/> to await</returns>
		public static async Task<TResult> RunAsyncFuncWithTimeoutAsync<TResult>(Func<CancellationToken, Task<TResult>> asyncFunc, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task<TResult>>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();

			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(asyncFunc(token));
				throw new SyntheticTimeoutException();
			}

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				return await asyncFunc(token).ConfigureAwait(false);
			}

			var ctsFunc = cancelIfTimeout
				? CancellationTokenSource.CreateLinkedTokenSource(token)
				: null;

			try
			{
				using (var ctsDelay = CancellationTokenSource.CreateLinkedTokenSource(token))
				{
					var funcTask = asyncFunc(ctsFunc?.Token ?? token);
					var delayTask = Task.Delay(timeout, ctsDelay.Token);

					await Task.WhenAny(funcTask, delayTask).ConfigureAwait(false);

					if (delayTask.IsCompleted == false && delayTask.IsFaulted == false)
					{
						ctsDelay.Cancel();
					}

					if (funcTask.IsCompleted == false && funcTask.IsFaulted == false)
					{
						ctsFunc?.Cancel();
					}

					token.ThrowIfCancellationRequested();

					if (funcTask.IsCompleted)
					{
						return await funcTask.ConfigureAwait(false);
					}

					if (cancelIfTimeout == false)
						timedOutTaskProcessor?.Invoke(funcTask);

					throw new SyntheticTimeoutException();
				}
			}
			finally
			{
				ctsFunc?.Dispose();
			}
		}

		/// <summary>
		/// Run an async action asynchronously with a timeout and some additional ooptions.
		/// </summary>
		/// <param name="asyncAction">The async action to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out action.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The resulting <see cref="Task"/> to await</returns>
		public static async Task RunAsyncActionWithTimeoutAsync(Func<CancellationToken, Task> asyncAction, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();

			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(asyncAction(token));
				throw new SyntheticTimeoutException();
			}

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				await asyncAction(token).ConfigureAwait(false);
				return;
			}

			var ctsFunc = cancelIfTimeout
				? CancellationTokenSource.CreateLinkedTokenSource(token)
				: null;

			try
			{
				using (var ctsDelay = CancellationTokenSource.CreateLinkedTokenSource(token))
				{
					var actionTask = asyncAction(ctsFunc?.Token ?? token);
					var delayTask = Task.Delay(timeout, ctsDelay.Token);

					await Task.WhenAny(actionTask, delayTask).ConfigureAwait(false);

					if (delayTask.IsCompleted == false && delayTask.IsFaulted == false)
					{
						ctsDelay.Cancel();
					}

					if (actionTask.IsCompleted == false && actionTask.IsFaulted == false)
					{
						ctsFunc?.Cancel();
					}

					token.ThrowIfCancellationRequested();

					if (actionTask.IsCompleted)
					{
						await actionTask.ConfigureAwait(false);
						return;
					}

					if (cancelIfTimeout == false)
						timedOutTaskProcessor?.Invoke(actionTask);

					throw new SyntheticTimeoutException();
				}
			}
			finally
			{
				ctsFunc?.Dispose();
			}
		}

		/// <summary>
		/// Run an async function synchoronously with a timeout and some additional ooptions.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="asyncFunc">The async function to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out function.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The value returned from the async function</returns>
		public static TResult RunAsyncFuncWithTimeout<TResult>(Func<CancellationToken, Task<TResult>> asyncFunc, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task<TResult>>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();

			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(asyncFunc(token));
				throw new SyntheticTimeoutException();
			}

			Task<TResult> task;

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				task = _taskFactory.StartNew(() => asyncFunc(token), token).Unwrap();
			}
			else
			{
				task = _taskFactory.StartNew(() => RunAsyncFuncWithTimeoutAsync(asyncFunc, timeout, cancelIfTimeout, timedOutTaskProcessor, token), token).Unwrap();
			}

			return task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Run an async action synchoronously with a timeout and some additional ooptions.
		/// </summary>
		/// <param name="asyncAction">The async action to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out action.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		public static void RunAsyncActionWithTimeout(Func<CancellationToken, Task> asyncAction, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(asyncAction(token));
				throw new SyntheticTimeoutException();
			}

			Task task;

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				task = _taskFactory.StartNew(() => asyncAction(token), token).Unwrap();
			}
			else
			{
				task = _taskFactory.StartNew(() => RunAsyncActionWithTimeoutAsync(asyncAction, timeout, cancelIfTimeout, timedOutTaskProcessor, token), token).Unwrap();
			}

			task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Run a sync function synchoronously with a timeout and some additional ooptions.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="syncFunc">The sync function to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out function.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The value returned from the sync function</returns>
		public static TResult RunSyncFuncWithTimeout<TResult>(Func<CancellationToken, TResult> syncFunc, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task<TResult>>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(_taskFactory.StartNew(() => syncFunc(token), token));
				throw new SyntheticTimeoutException();
			}

			Task<TResult> task;

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				//task = _taskFactory.StartNew(() => syncFunc(token), token);
				return syncFunc(token);
			}
			else
			{
				task = RunAsyncFuncWithTimeoutAsync(ct => _taskFactory.StartNew(() => syncFunc(ct), ct), timeout, cancelIfTimeout, timedOutTaskProcessor, token);
			}

			return task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Run a sync action synchoronously with a timeout and some additional ooptions.
		/// </summary>
		/// <param name="syncAction">The sync action to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="timedOutTaskProcessor">A lambda to process the task representing the eventually timed out action.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		public static void RunSyncActionWithTimeout(Action<CancellationToken> syncAction, TimeSpan timeout, bool cancelIfTimeout = true, Action<Task>? timedOutTaskProcessor = null, CancellationToken token = default)
		{
			if (timeout == TimeSpan.Zero || timeout < Timeout.InfiniteTimeSpan)
			{
				if (cancelIfTimeout == false)
					timedOutTaskProcessor?.Invoke(_taskFactory.StartNew(() => syncAction(token), token));
				throw new SyntheticTimeoutException();
			}

			Task task;

			if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
			{
				//task = _taskFactory.StartNew(() => syncAction(token), token);
				syncAction(token);
				return;
			}
			else
			{
				task = RunAsyncActionWithTimeoutAsync(ct => _taskFactory.StartNew(() => syncAction(ct), ct), timeout, cancelIfTimeout, timedOutTaskProcessor, token);
			}

			task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Run an async function with the ability to optionally set a timeout, await its completion (or run in a fire-and-forget way), process the eventually thrown exception or re-throw it.
		/// </summary>
		/// <param name="asyncAction">The async action to execute.</param>
		/// <param name="timeout">The timeout to apply.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="awaitCompletion">Indicates if the function's completion should be awaited or if the execution should be made in a fire-and-forget way.</param>
		/// <param name="exceptionProcessor">An exception processor for the exception that may be thrown.</param>
		/// <param name="reThrow">Indicates if, in case an exception is intercepted, it should be re-thrown.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		/// <returns>The resulting <see cref="Task"/> to await</returns>
		public static async Task RunAsyncActionAdvancedAsync(Func<CancellationToken, Task> asyncAction, TimeSpan timeout, bool cancelIfTimeout = true, bool awaitCompletion = true, Action<Exception>? exceptionProcessor = null, bool reThrow = false, CancellationToken token = default)
		{
			try
			{
				Task task;
				if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
				{
					task = asyncAction(token);
				}
				else
				{
					task = RunAsyncActionWithTimeoutAsync(asyncAction, timeout, cancelIfTimeout, token: token);
				}

				if (awaitCompletion)
				{
					await task.ConfigureAwait(false);
				}
				else
				{
					_ = task.ContinueWith(
						(antecedent, state) =>
						{
							if (antecedent.IsFaulted)
							{
								((Action<Exception>)state)?.Invoke(antecedent.Exception.GetSingleInnerExceptionOrSelf());
							}
						},
						exceptionProcessor
					);
				}
			}
			catch (Exception exc)
			{
				exceptionProcessor?.Invoke(exc);
				if (reThrow)
					throw;
			}
		}

		/// <summary>
		/// Run a sync action with the ability to optionally set a timeout, await its completion (or run in a fire-and-forget way), process the eventually thrown exception or re-throw it.
		/// </summary>
		/// <param name="syncAction">The sync action to execute.</param>
		/// <param name="timeout">The timeout to apply. Defaults to <see cref="Timeout.Infinite"/>.</param>
		/// <param name="cancelIfTimeout">Indicates if the action should be cancelled in case of a timeout.</param>
		/// <param name="awaitCompletion">Indicates if the action's completion should be awaited or if the execution should be made in a fire-and-forget way.</param>
		/// <param name="exceptionProcessor">An exception processor for the exception that may be thrown.</param>
		/// <param name="reThrow">Indicates if, in case an exception is intercepted, it should be re-thrown.</param>
		/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
		public static void RunSyncActionAdvanced(Action<CancellationToken> syncAction, TimeSpan timeout, bool cancelIfTimeout = true, bool awaitCompletion = true, Action<Exception>? exceptionProcessor = null, bool reThrow = false, CancellationToken token = default)
		{
			try
			{
				if (awaitCompletion)
				{
					if (timeout == Timeout.InfiniteTimeSpan && token == CancellationToken.None)
					{
						syncAction(token);
					}
					else
					{
						RunSyncActionWithTimeout(syncAction, timeout, cancelIfTimeout, token: token);
					}
				}
				else
				{
					_ = RunAsyncActionWithTimeoutAsync(ct => _taskFactory.StartNew(() => syncAction(ct), ct), timeout, cancelIfTimeout, token: token).ContinueWith(
						(antecedent, state) =>
						{
							if (antecedent.IsFaulted)
							{
								((Action<Exception>)state)?.Invoke(antecedent.Exception.GetSingleInnerExceptionOrSelf());
							}
						},
						exceptionProcessor
					);
				}
			}
			catch (Exception exc)
			{
				exceptionProcessor?.Invoke(exc);
				if (reThrow)
					throw;
			}
		}
	}
}
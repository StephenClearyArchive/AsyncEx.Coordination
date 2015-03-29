using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Nito.AsyncEx;
using Xunit;
using Nito.AsyncEx.Testing;

namespace UnitTests
{
    public class AsyncLockUnitTests
    {
        [Fact]
        public void AsyncLock_Unlocked_SynchronouslyPermitsLock()
        {
            var mutex = new AsyncLock();

            var lockTask = mutex.LockAsync().AsTask();

            Assert.True(lockTask.IsCompleted);
            Assert.False(lockTask.IsFaulted);
            Assert.False(lockTask.IsCanceled);
        }

        [Fact]
        public async Task AsyncLock_Locked_PreventsLockUntilUnlocked()
        {
            var mutex = new AsyncLock();
            var task1HasLock = new TaskCompletionSource<object>();
            var task1Continue = new TaskCompletionSource<object>();

            var task1 = Task.Run(async () =>
            {
                using (await mutex.LockAsync())
                {
                    task1HasLock.SetResult(null);
                    await task1Continue.Task;
                }
            });
            await task1HasLock.Task;

            var task2Start = Task.Factory.StartNew(async () =>
            {
                await mutex.LockAsync();
            });
            var task2 = await task2Start;

            Assert.False(task2.IsCompleted);
            task1Continue.SetResult(null);
            await task2;
        }

        [Fact]
        public async Task AsyncLock_DoubleDispose_OnlyPermitsOneTask()
        {
            var mutex = new AsyncLock();
            var task1HasLock = new TaskCompletionSource<object>();
            var task1Continue = new TaskCompletionSource<object>();

            await Task.Run(async () =>
            {
                var key = await mutex.LockAsync();
                key.Dispose();
                key.Dispose();
            });

            var task1 = Task.Run(async () =>
            {
                using (await mutex.LockAsync())
                {
                    task1HasLock.SetResult(null);
                    await task1Continue.Task;
                }
            });
            await task1HasLock.Task;

            var task2Start = Task.Factory.StartNew(async () =>
            {
                await mutex.LockAsync();
            });
            var task2 = await task2Start;

            Assert.False(task2.IsCompleted);
            task1Continue.SetResult(null);
            await task2;
        }

        [Fact]
        public async Task AsyncLock_Locked_OnlyPermitsOneLockerAtATime()
        {
            var mutex = new AsyncLock();
            var task1HasLock = new TaskCompletionSource<object>();
            var task1Continue = new TaskCompletionSource<object>();
            var task2HasLock = new TaskCompletionSource<object>();
            var task2Continue = new TaskCompletionSource<object>();

            var task1 = Task.Run(async () =>
            {
                using (await mutex.LockAsync())
                {
                    task1HasLock.SetResult(null);
                    await task1Continue.Task;
                }
            });
            await task1HasLock.Task;

            var task2Start = Task.Factory.StartNew(async () =>
            {
                using (await mutex.LockAsync())
                {
                    task2HasLock.SetResult(null);
                    await task2Continue.Task;
                }
            });
            var task2 = await task2Start;

            var task3Start = Task.Factory.StartNew(async () =>
            {
                await mutex.LockAsync();
            });
            var task3 = await task3Start;

            task1Continue.SetResult(null);
            await task2HasLock.Task;

            Assert.False(task3.IsCompleted);
            task2Continue.SetResult(null);
            await task2;
            await task3;
        }

        [Fact]
        public void AsyncLock_PreCancelled_Unlocked_SynchronouslyTakesLock()
        {
            var mutex = new AsyncLock();
            var token = new CancellationToken(true);

            var task = mutex.LockAsync(token).AsTask();

            Assert.True(task.IsCompleted);
            Assert.False(task.IsCanceled);
            Assert.False(task.IsFaulted);
        }

        [Fact]
        public void AsyncLock_PreCancelled_Locked_SynchronouslyCancels()
        {
            var mutex = new AsyncLock();
            var lockTask = mutex.LockAsync();
            var token = new CancellationToken(true);

            var task = mutex.LockAsync(token).AsTask();

            Assert.True(task.IsCompleted);
            Assert.True(task.IsCanceled);
            Assert.False(task.IsFaulted);
        }

        [Fact]
        public async Task AsyncLock_CancelledLock_LeavesLockUnlocked()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();
            var taskReady = new TaskCompletionSource<object>();

            var unlock = await mutex.LockAsync();
            var task = Task.Run(async () =>
            {
                var lockTask = mutex.LockAsync(cts.Token);
                taskReady.SetResult(null);
                await lockTask;
            });
            await taskReady.Task;
            cts.Cancel();
            await AsyncAssert.ThrowsAsync<OperationCanceledException>(task);
            Assert.True(task.IsCanceled);
            unlock.Dispose();

            var finalLockTask = mutex.LockAsync();
            await finalLockTask;
        }

        [Fact]
        public async Task AsyncLock_CanceledLock_ThrowsException()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();

            await mutex.LockAsync();
            var canceledLockTask = mutex.LockAsync(cts.Token).AsTask();
            cts.Cancel();

            await AsyncAssert.ThrowsAsync<OperationCanceledException>(canceledLockTask);
        }

        [Fact]
        public async Task AsyncLock_CanceledTooLate_StillTakesLock()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();

            AwaitableDisposable<IDisposable> cancelableLockTask;
            using (await mutex.LockAsync())
            {
                cancelableLockTask = mutex.LockAsync(cts.Token);
            }

            var key = await cancelableLockTask;
            cts.Cancel();

            var nextLocker = mutex.LockAsync().AsTask();
            Assert.False(nextLocker.IsCompleted);

            key.Dispose();
            await nextLocker;
        }

        [Fact]
        public void Id_IsNotZero()
        {
            var mutex = new AsyncLock();
            Assert.NotEqual(0, mutex.Id);
        }
    }
}

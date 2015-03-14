using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;

namespace UnitTests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class AsyncLockUnitTests
    {
        [TestMethod]
        public void AsyncLock_Unlocked_SynchronouslyPermitsLock()
        {
            var mutex = new AsyncLock();

            var lockTask = mutex.LockAsync().AsTask();

            Assert.IsTrue(lockTask.IsCompleted);
            Assert.IsFalse(lockTask.IsFaulted);
            Assert.IsFalse(lockTask.IsCanceled);
        }

        [TestMethod]
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

            Assert.IsFalse(task2.IsCompleted);
            task1Continue.SetResult(null);
            await task2;
        }

        [TestMethod]
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

            Assert.IsFalse(task2.IsCompleted);
            task1Continue.SetResult(null);
            await task2;
        }

        [TestMethod]
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

            Assert.IsFalse(task3.IsCompleted);
            task2Continue.SetResult(null);
            await task2;
            await task3;
        }

        [TestMethod]
        public void AsyncLock_PreCancelled_Unlocked_SynchronouslyTakesLock()
        {
            var mutex = new AsyncLock();
            var token = new CancellationToken(true);

            var task = mutex.LockAsync(token).AsTask();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

        [TestMethod]
        public void AsyncLock_PreCancelled_Locked_SynchronouslyCancels()
        {
            var mutex = new AsyncLock();
            var lockTask = mutex.LockAsync();
            var token = new CancellationToken(true);

            var task = mutex.LockAsync(token).AsTask();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

        [TestMethod]
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
            await AssertEx.ThrowsExceptionAsync<OperationCanceledException>(task);
            Assert.IsTrue(task.IsCanceled);
            unlock.Dispose();

            var finalLockTask = mutex.LockAsync();
            await finalLockTask;
        }

        [TestMethod]
        public async Task AsyncLock_CanceledLock_ThrowsException()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();

            await mutex.LockAsync();
            var canceledLockTask = mutex.LockAsync(cts.Token).AsTask();
            cts.Cancel();

            await AssertEx.ThrowsExceptionAsync<OperationCanceledException>(canceledLockTask);
        }

        [TestMethod]
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
            Assert.IsFalse(nextLocker.IsCompleted);

            key.Dispose();
            await nextLocker;
        }

        [TestMethod]
        public void Id_IsNotZero()
        {
            var mutex = new AsyncLock();
            Assert.AreNotEqual(0, mutex.Id);
        }
    }
}

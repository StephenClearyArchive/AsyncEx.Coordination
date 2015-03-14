using System;
using System.Threading.Tasks;
using Nito.AsyncEx;
using System.Linq;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class AsyncMonitorUnitTests
    {
        [TestMethod]
        public async Task Unlocked_PermitsLock()
        {
            var monitor = new AsyncMonitor();

            var task = monitor.EnterAsync();
            await task;
        }

        [TestMethod]
        public async Task Locked_PreventsLockUntilUnlocked()
        {
            var monitor = new AsyncMonitor();
            var task1HasLock = new TaskCompletionSource<object>();
            var task1Continue = new TaskCompletionSource<object>();

            var task1 = Task.Run(async () =>
            {
                using (await monitor.EnterAsync())
                {
                    task1HasLock.SetResult(null);
                    await task1Continue.Task;
                }
            });
            await task1HasLock.Task;

            var lockTask = monitor.EnterAsync().AsTask();
            Assert.IsFalse(lockTask.IsCompleted);
            task1Continue.SetResult(null);
            await lockTask;
        }

        [TestMethod]
        public async Task Pulse_ReleasesOneWaiter()
        {
            var monitor = new AsyncMonitor();
            int completed = 0;
            var task1Ready = new TaskCompletionSource<object>();
            var task2Ready = new TaskCompletionSource<object>();
            var task1 = Task.Run(async () =>
            {
                using (await monitor.EnterAsync())
                {
                    var waitTask1 = monitor.WaitAsync();
                    task1Ready.SetResult(null);
                    await waitTask1;
                    Interlocked.Increment(ref completed);
                }
            });
            await task1Ready.Task;
            var task2 = Task.Run(async () =>
            {
                using (await monitor.EnterAsync())
                {
                    var waitTask2 = monitor.WaitAsync();
                    task2Ready.SetResult(null);
                    await waitTask2;
                    Interlocked.Increment(ref completed);
                }
            });
            await task2Ready.Task;

            using (await monitor.EnterAsync())
            {
                monitor.Pulse();
            }
            await Task.WhenAny(task1, task2);
            var result = Interlocked.CompareExchange(ref completed, 0, 0);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task PulseAll_ReleasesAllWaiters()
        {
            var monitor = new AsyncMonitor();
            int completed = 0;
            var task1Ready = new TaskCompletionSource<object>();
            var task2Ready = new TaskCompletionSource<object>();
            Task waitTask1 = null;
            var task1 = Task.Run(async () =>
            {
                using (await monitor.EnterAsync())
                {
                    waitTask1 = monitor.WaitAsync();
                    task1Ready.SetResult(null);
                    await waitTask1;
                    Interlocked.Increment(ref completed);
                }
            });
            await task1Ready.Task;
            Task waitTask2 = null;
            var task2 = Task.Run(async () =>
            {
                using (await monitor.EnterAsync())
                {
                    waitTask2 = monitor.WaitAsync();
                    task2Ready.SetResult(null);
                    await waitTask2;
                    Interlocked.Increment(ref completed);
                }
            });
            await task2Ready.Task;

            var lockTask3 = monitor.EnterAsync();
            using (await lockTask3)
            {
                monitor.PulseAll();
            }
            await Task.WhenAll(task1, task2);
            var result = Interlocked.CompareExchange(ref completed, 0, 0);

            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public void Id_IsNotZero()
        {
            var monitor = new AsyncMonitor();
            Assert.AreNotEqual(0, monitor.Id);
        }
    }
}

using System;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using System.Linq;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class AsyncAutoResetEventUnitTests
    {
        [TestMethod]
        public async Task WaitAsync_Unset_IsNotCompleted()
        {
            var are = new AsyncAutoResetEvent();

            var task = are.WaitAsync();

            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public void WaitAsync_AfterSet_CompletesSynchronously()
        {
            var are = new AsyncAutoResetEvent();
            
            are.Set();
            var task = are.WaitAsync();
            
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public void WaitAsync_Set_CompletesSynchronously()
        {
            var are = new AsyncAutoResetEvent(true);

            var task = are.WaitAsync();
            
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public async Task MultipleWaitAsync_AfterSet_OnlyOneIsCompleted()
        {
            var are = new AsyncAutoResetEvent();

            are.Set();
            var task1 = are.WaitAsync();
            var task2 = are.WaitAsync();

            Assert.IsTrue(task1.IsCompleted);
            await AssertEx.NeverCompletesAsync(task2);
        }

        [TestMethod]
        public async Task MultipleWaitAsync_Set_OnlyOneIsCompleted()
        {
            var are = new AsyncAutoResetEvent(true);

            var task1 = are.WaitAsync();
            var task2 = are.WaitAsync();

            Assert.IsTrue(task1.IsCompleted);
            await AssertEx.NeverCompletesAsync(task2);
        }

        [TestMethod]
        public async Task MultipleWaitAsync_AfterMultipleSet_OnlyOneIsCompleted()
        {
            var are = new AsyncAutoResetEvent();

            are.Set();
            are.Set();
            var task1 = are.WaitAsync();
            var task2 = are.WaitAsync();

            Assert.IsTrue(task1.IsCompleted);
            await AssertEx.NeverCompletesAsync(task2);
        }

        [TestMethod]
        public void WaitAsync_PreCancelled_Set_SynchronouslyCompletesWait()
        {
            var are = new AsyncAutoResetEvent(true);
            var token = new CancellationToken(true);
            
            var task = are.WaitAsync(token);

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

        [TestMethod]
        public async Task WaitAsync_Cancelled_DoesNotAutoReset()
        {
            var are = new AsyncAutoResetEvent();
            var cts = new CancellationTokenSource();

            cts.Cancel();
            var task1 = are.WaitAsync(cts.Token);
            task1.WaitWithoutException();
            are.Set();
            var task2 = are.WaitAsync();

            await task2;
        }

        [TestMethod]
        public void WaitAsync_PreCancelled_Unset_SynchronouslyCancels()
        {
            var are = new AsyncAutoResetEvent(false);
            var token = new CancellationToken(true);

            var task = are.WaitAsync(token);

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

#if TODO
        [TestMethod]
        public void WaitAsyncFromCustomSynchronizationContext_PreCancelled_Unset_SynchronouslyCancels()
        {
            AsyncContext.Run(() =>
            {
                var are = new AsyncAutoResetEvent(false);
                var token = new CancellationToken(true);

                var task = are.WaitAsync(token);

                Assert.IsTrue(task.IsCompleted);
                Assert.IsTrue(task.IsCanceled);
                Assert.IsFalse(task.IsFaulted);
            });
        }
#endif

        [TestMethod]
        public async Task WaitAsync_Cancelled_ThrowsException()
        {
            var are = new AsyncAutoResetEvent();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = are.WaitAsync(cts.Token);
            await AssertEx.ThrowsExceptionAsync<OperationCanceledException>(task);
        }

        [TestMethod]
        public void Id_IsNotZero()
        {
            var are = new AsyncAutoResetEvent();
            Assert.AreNotEqual(0, are.Id);
        }
    }
}

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
    public class AsyncSemaphoreUnitTests
    {
        [TestMethod]
        public async Task WaitAsync_NoSlotsAvailable_IsNotCompleted()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreEqual(0, semaphore.CurrentCount);
            var task = semaphore.WaitAsync();
            Assert.AreEqual(0, semaphore.CurrentCount);
            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public async Task WaitAsync_SlotAvailable_IsCompleted()
        {
            var semaphore = new AsyncSemaphore(1);
            Assert.AreEqual(1, semaphore.CurrentCount);
            var task1 = semaphore.WaitAsync();
            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsTrue(task1.IsCompleted);
            var task2 = semaphore.WaitAsync();
            Assert.AreEqual(0, semaphore.CurrentCount);
            await AssertEx.NeverCompletesAsync(task2);
        }

        [TestMethod]
        public void WaitAsync_PreCancelled_SlotAvailable_SucceedsSynchronously()
        {
            var semaphore = new AsyncSemaphore(1);
            Assert.AreEqual(1, semaphore.CurrentCount);
            var token = new CancellationToken(true);

            var task = semaphore.WaitAsync(token);
            
            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

        [TestMethod]
        public void WaitAsync_PreCancelled_NoSlotAvailable_CancelsSynchronously()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreEqual(0, semaphore.CurrentCount);
            var token = new CancellationToken(true);

            var task = semaphore.WaitAsync(token);

            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsCanceled);
            Assert.IsFalse(task.IsFaulted);
        }

        [TestMethod]
        public async Task WaitAsync_Cancelled_DoesNotTakeSlot()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreEqual(0, semaphore.CurrentCount);
            var cts = new CancellationTokenSource();
            var task = semaphore.WaitAsync(cts.Token);
            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsFalse(task.IsCompleted);

            cts.Cancel();

            try { await task; }
            catch (OperationCanceledException) { }
            semaphore.Release();
            Assert.AreEqual(1, semaphore.CurrentCount);
            Assert.IsTrue(task.IsCanceled);
        }

        [TestMethod]
        public void Release_WithoutWaiters_IncrementsCount()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreEqual(0, semaphore.CurrentCount);
            semaphore.Release();
            Assert.AreEqual(1, semaphore.CurrentCount);
            var task = semaphore.WaitAsync();
            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public async Task Release_WithWaiters_ReleasesWaiters()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreEqual(0, semaphore.CurrentCount);
            var task = semaphore.WaitAsync();
            Assert.AreEqual(0, semaphore.CurrentCount);
            Assert.IsFalse(task.IsCompleted);
            semaphore.Release();
            Assert.AreEqual(0, semaphore.CurrentCount);
            await task;
        }

        [TestMethod]
        public void Release_Overflow_ThrowsException()
        {
            var semaphore = new AsyncSemaphore(int.MaxValue);
            Assert.AreEqual(int.MaxValue, semaphore.CurrentCount);
            AssertEx.ThrowsException<InvalidOperationException>(() => semaphore.Release());
        }

        [TestMethod]
        public void Release_ZeroSlots_HasNoEffect()
        {
            var semaphore = new AsyncSemaphore(1);
            Assert.AreEqual(1, semaphore.CurrentCount);
            semaphore.Release(0);
            Assert.AreEqual(1, semaphore.CurrentCount);
        }

        [TestMethod]
        public void Id_IsNotZero()
        {
            var semaphore = new AsyncSemaphore(0);
            Assert.AreNotEqual(0, semaphore.Id);
        }
    }
}

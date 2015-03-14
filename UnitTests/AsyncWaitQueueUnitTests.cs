using System;
using System.Diagnostics.CodeAnalysis;
using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class AsyncWaitQueueUnitTests
    {
        [TestMethod]
        public void IsEmpty_WhenEmpty_IsTrue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            Assert.IsTrue(queue.IsEmpty);
        }

        [TestMethod]
        public void IsEmpty_WithOneItem_IsFalse()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            queue.Enqueue();
            Assert.IsFalse(queue.IsEmpty);
        }

        [TestMethod]
        public void IsEmpty_WithTwoItems_IsFalse()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            queue.Enqueue();
            queue.Enqueue();
            Assert.IsFalse(queue.IsEmpty);
        }

        [TestMethod]
        public void Dequeue_SynchronouslyCompletesTask()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task = queue.Enqueue();
            queue.Dequeue();
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public async Task Dequeue_WithTwoItems_OnlyCompletesFirstItem()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task1 = queue.Enqueue();
            var task2 = queue.Enqueue();
            queue.Dequeue();
            Assert.IsTrue(task1.IsCompleted);
            await AssertEx.NeverCompletesAsync(task2);
        }

        [TestMethod]
        public void Dequeue_WithResult_SynchronouslyCompletesWithResult()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var result = new object();
            var task = queue.Enqueue();
            queue.Dequeue(result);
            Assert.AreSame(result, task.Result);
        }

        [TestMethod]
        public void Dequeue_WithoutResult_SynchronouslyCompletesWithDefaultResult()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task = queue.Enqueue();
            queue.Dequeue();
            Assert.AreEqual(default(object), task.Result);
        }

        [TestMethod]
        public void DequeueAll_SynchronouslyCompletesAllTasks()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task1 = queue.Enqueue();
            var task2 = queue.Enqueue();
            queue.DequeueAll();
            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);
        }

        [TestMethod]
        public void DequeueAll_WithoutResult_SynchronouslyCompletesAllTasksWithDefaultResult()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task1 = queue.Enqueue();
            var task2 = queue.Enqueue();
            queue.DequeueAll();
            Assert.AreEqual(default(object), task1.Result);
            Assert.AreEqual(default(object), task2.Result);
        }

        [TestMethod]
        public void DequeueAll_WithResult_CompletesAllTasksWithResult()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var result = new object();
            var task1 = queue.Enqueue();
            var task2 = queue.Enqueue();
            queue.DequeueAll(result);
            Assert.AreSame(result, task1.Result);
            Assert.AreSame(result, task2.Result);
        }

        [TestMethod]
        public void TryCancel_EntryFound_SynchronouslyCancelsTask()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task = queue.Enqueue();
            queue.TryCancel(task, new CancellationToken(true));
            Assert.IsTrue(task.IsCanceled);
        }

        [TestMethod]
        public void TryCancel_EntryFound_RemovesTaskFromQueue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task = queue.Enqueue();
            queue.TryCancel(task, new CancellationToken(true));
            Assert.IsTrue(queue.IsEmpty);
        }

        [TestMethod]
        public void TryCancel_EntryNotFound_DoesNotRemoveTaskFromQueue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var task = queue.Enqueue();
            queue.Enqueue();
            queue.Dequeue();
            queue.TryCancel(task, new CancellationToken(true));
            Assert.IsFalse(queue.IsEmpty);
        }

        [TestMethod]
        public async Task Cancelled_WhenInQueue_CancelsTask()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var cts = new CancellationTokenSource();
            var task = queue.Enqueue(new object(), cts.Token);
            cts.Cancel();
            await AssertEx.ThrowsExceptionAsync<OperationCanceledException>(task);
        }

        [TestMethod]
        public async Task Cancelled_WhenInQueue_RemovesTaskFromQueue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var cts = new CancellationTokenSource();
            var task = queue.Enqueue(new object(), cts.Token);
            cts.Cancel();
            await AssertEx.ThrowsExceptionAsync<OperationCanceledException>(task);
            Assert.IsTrue(queue.IsEmpty);
        }

        [TestMethod]
        public void Cancelled_WhenNotInQueue_DoesNotRemoveTaskFromQueue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var cts = new CancellationTokenSource();
            var task = queue.Enqueue(new object(), cts.Token);
            var _ = queue.Enqueue();
            queue.Dequeue();
            cts.Cancel();
            Assert.IsFalse(queue.IsEmpty);
        }

        [TestMethod]
        public void Cancelled_BeforeEnqueue_SynchronouslyCancelsTask()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = queue.Enqueue(new object(), cts.Token);
            Assert.IsTrue(task.IsCanceled);
        }

        [TestMethod]
        public void Cancelled_BeforeEnqueue_RemovesTaskFromQueue()
        {
            var queue = new DefaultAsyncWaitQueue<object>() as IAsyncWaitQueue<object>;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = queue.Enqueue(new object(), cts.Token);
            Assert.IsTrue(queue.IsEmpty);
        }
    }
}

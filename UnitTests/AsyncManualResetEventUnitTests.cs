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
    public class AsyncManualResetEventUnitTests
    {
        [TestMethod]
        public async Task WaitAsync_Unset_IsNotCompleted()
        {
            var mre = new AsyncManualResetEvent();

            var task = mre.WaitAsync();

            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public async Task Wait_Unset_IsNotCompleted()
        {
            var mre = new AsyncManualResetEvent();

            var task = Task.Run(() => mre.Wait());

            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public void WaitAsync_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            var task = mre.WaitAsync();
            
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public void Wait_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            mre.Wait();
        }

        [TestMethod]
        public void WaitAsync_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            var task = mre.WaitAsync();
            
            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public void Wait_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            mre.Wait();
        }

        [TestMethod]
        public void MultipleWaitAsync_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            var task1 = mre.WaitAsync();
            var task2 = mre.WaitAsync();
            
            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);
        }

        [TestMethod]
        public void MultipleWait_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            mre.Wait();
            mre.Wait();
        }

        [TestMethod]
        public void MultipleWaitAsync_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            var task1 = mre.WaitAsync();
            var task2 = mre.WaitAsync();
            
            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);
        }

        [TestMethod]
        public void MultipleWait_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            mre.Wait();
            mre.Wait();
        }

        [TestMethod]
        public async Task WaitAsync_AfterReset_IsNotCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            mre.Reset();
            var task = mre.WaitAsync();

            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public async Task Wait_AfterReset_IsNotCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            mre.Reset();
            var task = Task.Run(() => mre.Wait());

            await AssertEx.NeverCompletesAsync(task);
        }

        [TestMethod]
        public void Id_IsNotZero()
        {
            var mre = new AsyncManualResetEvent();
            Assert.AreNotEqual(0, mre.Id);
        }
    }
}

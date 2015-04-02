using System;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    /// <summary>
    /// A source for deferrals. Event argument types may implement this interface to indicate they understand async event handlers.
    /// </summary>
    public interface IDeferralSource
    {
        /// <summary>
        /// Requests a deferral. When the deferral is disposed, it is considered complete.
        /// </summary>
        IDisposable GetDeferral();
    }

    internal interface IDeferralManager
    {
        /// <summary>
        /// Increments the count of active deferrals for this manager.
        /// </summary>
        void IncrementCount();

        /// <summary>
        /// Decrements the count of active deferrals for this manager. If the count reaches <c>0</c>, then the manager notifies the code raising the event.
        /// </summary>
        void DecrementCount();
    }

    /// <summary>
    /// Manages the deferrals for an event that may have asynchonous handlers and needs to know when they complete. Instances of this type may not be reused.
    /// </summary>
    public sealed class DeferralManager : IDeferralManager
    {
        /// <summary>
        /// The deferral source for deferrals managed by this manager.
        /// </summary>
        private readonly IDeferralSource _source;

        /// <summary>
        /// The lock protecting <see cref="_count"/> and <see cref="_tcs"/>.
        /// </summary>
        private readonly object _mutex;

        /// <summary>
        /// The number of active deferrals.
        /// </summary>
        private int _count;

        /// <summary>
        /// Completed when <see cref="_count"/> reaches <c>0</c>. May be <c>null</c> if <see cref="_count"/> was never incremented.
        /// </summary>
        private TaskCompletionSource<object> _tcs;

        /// <summary>
        /// Creates a new deferral manager.
        /// </summary>
        public DeferralManager()
        {
            _source = new ManagedDeferralSource(this);
            _mutex = new object();
        }

        void IDeferralManager.IncrementCount()
        {
            lock (_mutex)
            {
                if (_tcs == null)
                    _tcs = new TaskCompletionSource<object>();
                ++_count;
            }
        }

        void IDeferralManager.DecrementCount()
        {
            lock (_mutex)
            {
                --_count;
                if (_count != 0)
                    return;
            }
            _tcs.TrySetResult(null);
        }

        /// <summary>
        /// Gets a source for deferrals managed by this deferral manager. This is generally used to implement <see cref="IDeferralSource"/> for event argument types.
        /// </summary>
        public IDeferralSource DeferralSource { get { return _source; } }

        /// <summary>
        /// Notifies the manager that all deferral requests have been made, and returns a task that is completed when all deferrals have completed.
        /// </summary>
        public Task WaitForDeferralsAsync()
        {
            lock (_mutex)
            {
                if (_tcs == null)
                    return TaskConstants.Completed;
                return _tcs.Task;
            }
        }

        /// <summary>
        /// A source for deferrals.
        /// </summary>
        private sealed class ManagedDeferralSource : IDeferralSource
        {
            /// <summary>
            /// The deferral manager in charge of this deferral source.
            /// </summary>
            private readonly IDeferralManager _manager;

            public ManagedDeferralSource(IDeferralManager manager)
            {
                _manager = manager;
            }

            IDisposable IDeferralSource.GetDeferral()
            {
                _manager.IncrementCount();
                return new Deferral(_manager);
            }

            /// <summary>
            /// A deferral.
            /// </summary>
            private sealed class Deferral : IDisposable
            {
                /// <summary>
                /// The deferral manager in charge of this deferral.
                /// </summary>
                private IDeferralManager _manager;

                public Deferral(IDeferralManager manager)
                {
                    _manager = manager;
                }

                /// <summary>
                /// Completes the deferral.
                /// </summary>
                void IDisposable.Dispose()
                {
                    if (_manager != null)
                        _manager.DecrementCount();
                    _manager = null;
                }
            }
        }
    }
}
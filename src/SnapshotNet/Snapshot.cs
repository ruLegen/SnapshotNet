using SnapshotNet.Extensions;
using System;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SnapshotNet
{
    public abstract class Snapshot : IDisposable
    {
        public const int INVALID_ID = 0;
        private static object _lock = new object();
        private static ThreadLocal<Snapshot?> _threadSnapshot = new ThreadLocal<Snapshot?>();
        private static int _nextSnapshotId = INVALID_ID + 1;
        public static int NextSnapshotId
        {
            get { lock (_lock) { return _nextSnapshotId; } }
            set { lock (_lock) { _nextSnapshotId = value; } }
        }
        protected static Snapshot CurrentGlobalSnapshot;
        static Snapshot()
        {
            CurrentGlobalSnapshot = new GlobalSnapshot(NextSnapshotId++, new HashSet<int>());
            OpenSnapshots = OpenSnapshots.CloneAndAdd(CurrentGlobalSnapshot.Id);
        }

        protected static HashSet<int> OpenSnapshots { get; set; } = new HashSet<int>();

        public int Id { get; protected set; } = INVALID_ID;
        public HashSet<int> InvalidSet { get; set; } = new HashSet<int>();
        protected bool disposed;

        public abstract Snapshot Root { get; }
        public abstract bool ReadOnly { get; }

        /*
         * The read observer for the snapshot if there is one.
         */
        internal abstract Action<object> ReadObserver { get; set; }

        /**
         * The write observer for the snapshot if there is one.
         */
        internal abstract Action<object> WriteObserver { get; set; }


        public Snapshot(int id, HashSet<int> invalidSet)
        {
            Id = id;
            InvalidSet = invalidSet;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(Action block)
        {
            var previous = makeCurrent();
            try
            {
                block();
            }
            finally
            {
                restoreCurrent(previous);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Enter<T>(Func<T> block)
        {
            var previous = makeCurrent();
            try
            {
                return block();
            }
            finally
            {
                restoreCurrent(previous);
            };
        }
        internal Snapshot? makeCurrent()
        {
            var previous = _threadSnapshot.Value;
            _threadSnapshot.Value = this;
            return previous;
        }
        internal void restoreCurrent(Snapshot? snapshot)
        {
            _threadSnapshot.Value = snapshot;
        }
        internal Snapshot? unsafeEnter() => makeCurrent();

        internal void unsafeLeave(Snapshot? oldSnapshot)
        {
            checkPrecondition(_threadSnapshot.Value == this, () =>
                $"Cannot leave snapshot; {this} is not the current snapshot"
            );
            restoreCurrent(oldSnapshot);
        }

        public void validateNotDisposed()
        {
            checkPrecondition(!disposed, () => "Cannot use a disposed snapshot");
        }
        public void validateNotAppliedOrPinned()
        {
            //checkPrecondition(!applied || isPinned, () => "Unsupported operation on a disposed or applied snapshot");

        }
        public virtual void Dispose()
        {
            disposed = true;
            lock (this)
            {
                //TODO implement
                //releasePinnedSnapshotLocked();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Sync(Action action)
        {
            lock (_lock)
            {
                action();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T Sync<T>(Func<T> action)
        {
            lock (_lock)
            {
                return action();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void checkPrecondition(bool value, Func<string> lazyMsg)
        {
            if (!value)
                throw new InvalidOperationException(lazyMsg());
        }

        public static Snapshot Current()
        {
            return _threadSnapshot.Value ?? CurrentGlobalSnapshot;
        }

        public static Snapshot TakeMutableSnapshot(/*observers*/)
        {
            if (Current() is MutableSnapshot ms)
            {
                return ms.TakeNestedMutableSnapshot();
            }
            throw new InvalidOperationException("Cannot create a mutable snapshot of an read-only snapshot");
        }


        protected static Action<object> MergedReadObserver(Action<object>? readObserver, Action<object>? parentReadObserver, bool mergeReadObserver = true)
        {

            var parentObserver = mergeReadObserver ? parentReadObserver : null;
            if (readObserver != null && parentObserver != null && readObserver != parentObserver)
            {
                return (state) =>
                {
                    readObserver(state);
                    parentObserver(state);
                };
            }
            else
                return readObserver ?? parentObserver;
        }

        protected static Action<object>? MergedWriteObserver(Action<object>? writeObserver, Action<object>? parentObserver)
        {
            if (writeObserver != null && parentObserver != null && writeObserver != parentObserver)
            {
                return (state) =>
                {
                    writeObserver(state);
                    parentObserver(state);
                };
            }
            else
                return writeObserver ?? parentObserver;
        }
    }
}

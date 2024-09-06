using System;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace SnapshotNet
{
    public sealed class Snapshot : IDisposable
    {
        static ThreadLocal<Snapshot?> threadSnapshot = new ThreadLocal<Snapshot?>();

        public const int INVALID_ID = -1;
        public int Id { get; private set; } = INVALID_ID;
        public IReadOnlySet<int> InvalidSet => _invalidSet;

        private HashSet<int> _invalidSet = new HashSet<int>();
        private bool disposed;

        public Snapshot(int id, HashSet<int> invalidSet)
        {
            Id = id;
            _invalidSet = invalidSet;
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
            var previous = threadSnapshot.Value;
            threadSnapshot.Value = this;
            return previous;
        }
        internal void restoreCurrent(Snapshot? snapshot)
        {
            threadSnapshot.Value = snapshot;
        }
        internal Snapshot? unsafeEnter() => makeCurrent();

        internal void unsafeLeave(Snapshot? oldSnapshot)
        {
            checkPrecondition(threadSnapshot.Value == this, () =>
                $"Cannot leave snapshot; {this} is not the current snapshot"
            );
            restoreCurrent(oldSnapshot);
        }

       
        public void Dispose()
        {
            disposed = true;
            lock (this)
            {
                //TODO implement
                //releasePinnedSnapshotLocked();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void checkPrecondition(bool value, Func<string> lazyMsg)
        {
            if (!value)
                throw new InvalidOperationException(lazyMsg());
        }
    }
}

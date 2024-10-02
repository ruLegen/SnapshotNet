using SnapshotNet.Extensions;
using SnapshotNet.Utils;
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
        internal static SnapshotDoubleIndexHeap PinningTable = new SnapshotDoubleIndexHeap();
        internal static WeakHashSet<IStateObject> ExtraStateObjects = new WeakHashSet<IStateObject> ();

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
        internal abstract int WriteCount { get; set; }

        internal abstract HashSet<IStateObject> Modified { get; set; }


        protected bool isPinned => _pinningTrackingHandle >= 0;

        protected int _pinningTrackingHandle = INVALID_ID;
        public Snapshot(int id, HashSet<int> invalidSet)
        {
            Id = id;
            InvalidSet = invalidSet;
            _pinningTrackingHandle = id != INVALID_ID ? TrackPinning(id, InvalidSet) : -1;
        }

        private int TrackPinning(int id, HashSet<int> invalidSet)
        {
            var pinned = invalidSet.Count == 0 ? id : invalidSet.Min();
            return Sync(() =>
            {
                return PinningTable.Add(pinned);
            });
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
        internal abstract void RecordModified(IStateObject state);

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
        internal static void Sync(Action action)
        {
            lock (_lock)
            {
                action();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Sync<T>(Func<T> action)
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

        internal static T? ReadableSilent<T>(T r, int id, HashSet<int> invalid) where T : StateRecord
        {
            // The readable record is the valid record with the highest snapshotId
            StateRecord? current = r;
            StateRecord? candidate = null;
            while (current != null)
            {
                if (Valid(current, id, invalid))
                {
                    candidate = candidate == null ? current
                                                    : ((candidate.SnapshotId < current.SnapshotId) ? current
                                                                                                    : candidate);
                }
                current = current.Next;
            }
            if (candidate != null)
            {
                return candidate as T;
            }
            return null;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Valid(int currentSnapshot, int candidateSnapshot, HashSet<int> invalid)
        {
            return candidateSnapshot != INVALID_ID && candidateSnapshot <= currentSnapshot && !invalid.Contains(candidateSnapshot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Valid(StateRecord data, int snapshot, HashSet<int> invalid) => Valid(snapshot, data.SnapshotId, invalid);


        internal static T Current<T>(T r, Snapshot snapshot) where T : StateRecord
            => ReadableSilent(r, snapshot.Id, snapshot.InvalidSet) ?? ReadError<T>();
        internal static T Current<T>(T r) where T : StateRecord
        {
            var res = Snapshot.Current().Let(snapshot => ReadableSilent(r, snapshot.Id, snapshot.InvalidSet) ??
                    Sync(() => Snapshot.Current().Let(syncSnapshot => ReadableSilent(r, syncSnapshot.Id, syncSnapshot.InvalidSet))));
            return res ?? ReadError<T>();
        }

        internal static void NotifyWrite(Snapshot snapshot, IStateObject stateObject)
        {
            snapshot.WriteCount += 1;
            snapshot?.WriteObserver?.Invoke(stateObject);
        }

        internal static StateRecord? UsedLocked(IStateObject state)
        {
            var current = state.FirstStateRecord;
            StateRecord? validRecord = null;

            var reuseLimit = PinningTable.LowestOrDefault(NextSnapshotId) - 1;

            var invalid = new HashSet<int>();
            while (current != null)
            {
                var currentId = current.SnapshotId;
                if (currentId == INVALID_ID)
                {
                    // Any records that were marked invalid by an abandoned snapshot or is marked reachable
                    // can be used immediately.
                    return current;
                }
                if (Valid(current, reuseLimit, invalid))
                {
                    if (validRecord == null)
                    {
                        validRecord = current;
                    }
                    else
                    {
                        // If we have two valid records one must obscure the other. Return the
                        // record with the lowest id
                        return current.SnapshotId < validRecord.SnapshotId ? current : validRecord;
                    }
                }
                current = current.Next;
            }
            return null;
        }

        internal static T ReadError<T>()
        {
            throw new Exception("Reading a state that was created after the snapshot was taken or in a snapshot that " +
                "has not yet been applied");

        }
    }
}


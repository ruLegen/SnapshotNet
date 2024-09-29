using SnapshotNet.Extensions;

namespace SnapshotNet
{
    public class GlobalSnapshot : MutableSnapshot
    {

        public GlobalSnapshot(int id, HashSet<int> invalidSet) : base(id, invalidSet, null, null, null)
        {
        }
        private T takeNewSnapshot<T>(Func<HashSet<int>, T> block) where T : Snapshot
        {
            return AdvanceGlobalSnapshot((invalid) =>
            {

                var result = block(invalid);
                Sync(() => OpenSnapshots = OpenSnapshots.CloneAndAdd(result.Id));
                return result;
            });
        }
        public override Snapshot TakeNestedMutableSnapshot(Action<object>? readObserver = null, Action<object>? writeObserver = null)
        {
            return takeNewSnapshot((invalid) =>
            {
                return new MutableSnapshot(Sync(() => NextSnapshotId++), invalid.Clone(), readObserver, writeObserver);
            });
        }

        private T AdvanceGlobalSnapshot<T>(Func<HashSet<int>, T> block)
        {
            var previousGlobalSnapshot = CurrentGlobalSnapshot;

            HashSet<int> modified = null; // Effectively val; can be with contracts
            var result = Sync(() =>
            {
                previousGlobalSnapshot = CurrentGlobalSnapshot;
                //modified = previousGlobalSnapshot.modified;
                //if (modified != null)
                //{
                //    pendingApplyObserverCount.add(1)
                //}
                return takeNewGlobalSnapshot(previousGlobalSnapshot, block);
            });

            //// If the previous global snapshot had any modified states then notify the registered apply
            //// observers.
            //modified?.let {
            //    try
            //    {
            //        val observers = applyObservers
            //        observers.fastForEach {
            //            observer->
            //    observer(it, previousGlobalSnapshot)
            //}
            //    }
            //    finally
            //    {
            //        pendingApplyObserverCount.add(-1)
            //    }
            //}

            //Sync {
            //    checkAndOverwriteUnusedRecordsLocked()
            //    modified?.fastForEach { processForUnusedRecordsLocked(it) }
            //}

            return result;
        }
        private T takeNewGlobalSnapshot<T>(Snapshot previousGlobalSnapshot, Func<HashSet<int>, T> block)
        {
            // Deactivate global snapshot. It is safe to just deactivate it because it cannot have
            // any conflicting writes as it is always closed before another snapshot is taken.
            var result = block(OpenSnapshots.CloneAndRemove(previousGlobalSnapshot.Id));
            Sync(() =>
            {
                var globalId = NextSnapshotId++;
                OpenSnapshots = OpenSnapshots.CloneAndRemove(previousGlobalSnapshot.Id);
                CurrentGlobalSnapshot = new GlobalSnapshot(globalId, OpenSnapshots.Clone());
                previousGlobalSnapshot.Dispose();
                OpenSnapshots = OpenSnapshots.CloneAndAdd(globalId);
            });
            return result;
        }
    }
}

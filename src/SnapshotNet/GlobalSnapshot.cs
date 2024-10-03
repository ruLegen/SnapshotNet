using SnapshotNet.Extensions;
using System;

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

            HashSet<IStateObject> modified = null; // Effectively val; can be with contracts
            var result = Sync(() =>
            {
                previousGlobalSnapshot = CurrentGlobalSnapshot;
                modified = previousGlobalSnapshot.Modified;
                if (modified != null)
                {
                    //pendingApplyObserverCount.add(1);
                }
                return takeNewGlobalSnapshot(previousGlobalSnapshot, block);
            });

            //// If the previous global snapshot had any modified states then notify the registered apply
            //// observers.
            //modified?.Let(it=>{
            //    try
            //    {
            //        var observers = ApplyObservers;
            //        observers.fastForEach
            //            observer->
            //    observer(it, previousGlobalSnapshot)
            //}
            //    }
            //    finally
            //    {
            //        pendingApplyObserverCount.add(-1)
            //    }
            //}

            Sync(() =>
            {
                checkAndOverwriteUnusedRecordsLocked();
                if (modified == null)
                    return;
                foreach (var i in modified)
                {
                    processForUnusedRecordsLocked(i);
                }
            });

            return result;
        }
        public override void Dispose()
        {
            Sync(() => {
                ReleasePinnedSnapshotLocked();
            });
        }
        private void checkAndOverwriteUnusedRecordsLocked()
        {
            ExtraStateObjects.RemoveIf((it) => {
               return !OverwriteUnusedRecordsLocked(it);
            });
        }

        private bool OverwriteUnusedRecordsLocked(IStateObject state)
        {
            StateRecord current = state.FirstStateRecord;
            StateRecord overwriteRecord = null;
            StateRecord validRecord = null;
            int reuseLimit = PinningTable.LowestOrDefault(NextSnapshotId);
            int retainedRecords = 0;

            while (current != null)
            {
                int currentId = current.SnapshotId;
                if (currentId != INVALID_ID)
                {
                    if (currentId < reuseLimit)
                    {
                        if (validRecord == null)
                        {
                            // If any records are below [reuseLimit], keep the highest one
                            // so the lowest snapshot can select it.
                            validRecord = current;
                            retainedRecords++;
                        }
                        else
                        {
                            // If [validRecord] is from an earlier snapshot, overwrite it instead.
                            StateRecord recordToOverwrite = current.SnapshotId < validRecord.SnapshotId
                                ? current
                                : validRecord;

                            if (recordToOverwrite == validRecord)
                            {
                                validRecord = current;
                            }

                            if (overwriteRecord == null)
                            {
                                // Find a record we will definitely keep
                                overwriteRecord = state.FirstStateRecord.FindYoungestOr(
                                    r => r.SnapshotId >= reuseLimit
                                );
                            }

                            recordToOverwrite.SnapshotId = INVALID_ID;
                            recordToOverwrite.Assign(overwriteRecord);
                        }
                    }
                    else
                    {
                        retainedRecords++;
                    }
                }

                current = current.Next;
            }

            return retainedRecords > 1;
        }


        private void processForUnusedRecordsLocked(IStateObject state)
        {
            if (OverwriteUnusedRecordsLocked(state))
            {
                ExtraStateObjects.Add(state);
            }
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

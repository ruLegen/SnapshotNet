using SnapshotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet.Extensions
{
    public static class StateRecordExtensions
    {
        public static R WithCurrent<T, R>(this T curr, Func<T, R> block) where T : StateRecord
        {
            return block(Snapshot.Current(curr));

        }

        /**
 * Return the current readable state record for the current snapshot. It is assumed that [this]
 * is the first record of [state]
 */
        public static T Readable<T>(this T curr, IStateObject state) where T : StateRecord
        {
            var snapshot = Snapshot.Current();
            snapshot.ReadObserver?.Invoke(state);
            return Snapshot.ReadableSilent(curr, snapshot.Id, snapshot.InvalidSet) ?? Snapshot.Sync(() =>
            {
                // Readable can return null when the global snapshot has been advanced by another thread
                // and state written to the object was overwritten while this thread was paused. Repeating
                // the read is valid here as either this will return the same result as the previous call
                // or will find a valid record. Being in a sync block prevents other threads from writing
                // to this state object until the read completes.
                var syncSnapshot = Snapshot.Current();
                return Snapshot.ReadableSilent<T>(state.FirstStateRecord as T, syncSnapshot.Id, syncSnapshot.InvalidSet) ?? Snapshot.ReadError<T>();
            });
        }

        /**
         * Return the current readable state record for the [snapshot]. It is assumed that [this]
         * is the first record of [state]
         */
        public static T Readable<T>(this T curr, IStateObject state, Snapshot snapshot) where T : StateRecord
        {
            // invoke the observer associated with the current snapshot.
            snapshot.ReadObserver?.Invoke(state);
            return Snapshot.ReadableSilent(curr, snapshot.Id, snapshot.InvalidSet) ?? Snapshot.ReadError<T>();
        }

        internal static StateRecord FindYoungestOr(this StateRecord curr,Func<StateRecord, bool> predicate)
        {
            StateRecord current = curr;
            StateRecord youngest = curr;

            while (current != null)
            {
                if (predicate(current))
                    return current;

                if (youngest.SnapshotId < current.SnapshotId)
                    youngest = current;

                current = current.Next;
            }

            return youngest;
        }
    }
}

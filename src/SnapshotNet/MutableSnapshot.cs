using SnapshotNet.Extensions;
using System;

namespace SnapshotNet
{
    public class MutableSnapshot : Snapshot
    {
        public HashSet<int> PreviousIds = new HashSet<int>();
        public bool applied { get; private set; } = false;

        public override Snapshot Root => this;
        public override bool ReadOnly => false;

        internal override Action<object>? ReadObserver { get; set; }

        internal override Action<object>? WriteObserver { get; set; }

        private int snapshots = 1;
        public MutableSnapshot(int id, HashSet<int> invalidSet, Action<object>? readObserver = null, Action<object>? writeObserver = null, Snapshot? parent = null) : base(id, invalidSet)
        {
            ReadObserver = readObserver;
            WriteObserver = writeObserver;
        }

        public virtual Snapshot TakeNestedMutableSnapshot(Action<object>? readObserver = null, Action<object>? writeObserver = null)
        {
            validateNotDisposed();
            validateNotAppliedOrPinned();
            return Advance(() =>
            {
                return Sync(() =>
                 {
                     var newId = NextSnapshotId++;
                     OpenSnapshots = OpenSnapshots.CloneAndAdd(newId);
                     var currentInvalid = InvalidSet;
                     InvalidSet = currentInvalid.CloneAndAdd(newId);
                     return new NestedMutableSnapshot(
                         newId,
                         currentInvalid.CloneAndAddRange(Id + 1, newId),
                         MergedReadObserver(readObserver, this.ReadObserver),
                         MergedWriteObserver(writeObserver, this.WriteObserver),
                         this
                     );
                 });
            });
        }
        /// <summary>
        /// Advances parent snapshot id
        /// </summary>
        /// <param name="value">returns child snapshot</param>
        /// <returns>>returns child snapshot</returns>
        private T Advance<T>(Func<T> value)
        {
            recordPrevious(Id);
            var result = value();
            if (!applied && !disposed)
            {
                var previousId = Id;
                Sync(() =>
                {
                    Id = NextSnapshotId++;
                    OpenSnapshots = OpenSnapshots.CloneAndAdd(Id);
                });
                InvalidSet = InvalidSet.CloneAndAddRange(previousId + 1, Id);
            }
            return result;
        }

        private void Advance()
        {
            Advance<object>(() => null);
        }

        public void recordPrevious(int id)
        {
            Sync(() =>
            {
                PreviousIds = PreviousIds.CloneAndAdd(id);
            });
        }

        public void ActivateNestedSnapshot()
        {
            snapshots++;
        }

        public void nestedDeactivated(Snapshot snapshot)
        {
            checkPrecondition(snapshots > 0, () => "no pending nested snapshots");
            if (--snapshots == 0)
            {
                if (!applied)
                {
                    //abandon();
                }
            }
        }

        public override void Dispose()
        {
            if (!disposed)
            {
                base.Dispose();
                nestedDeactivated(this);
            }
        }
    }
}

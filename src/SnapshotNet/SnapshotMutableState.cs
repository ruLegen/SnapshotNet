#pragma warning disable CS8602 // Dereference of a possibly null reference.
using SnapshotNet.Extensions;

namespace SnapshotNet
{
    public class SnapshotMutableState<T> : IStateObject, ISnapshotMutableState<T>
    {
        public StateRecord FirstStateRecord => _next;
        public T Value
        {
            get => _next.Readable(this).Value;
            set
            {
                _next.WithCurrent((it) =>
                {
                    if (!Policy.Equivalent(it.Value, value))
                    {
                        _next.Overwritable(this, it, (rec) => 
                        { 
                            rec.Value = value; 
                            return rec; 
                        });
                    }
                    return it;
                });
            }
        }
        public ISnapShotMutationPolicy<T> Policy { get; set; }

        T IState<T>.Value => Value;
        private StateStateRecord<T> _next;
        public SnapshotMutableState(T value, ISnapShotMutationPolicy<T> policy)
        {
            Policy = policy;
            _next = new StateStateRecord<T>(Snapshot.Current().Id,value);
        }
        public void PrependStateRecord(StateRecord value)
        {
#pragma warning disable CS8601 
            _next = (value as StateStateRecord<T>);
#pragma warning restore CS8601 
        }

        public override string? ToString()
        {
            return _next.WithCurrent((it) => $"MutableState(value={it.Value}){GetHashCode()}");
        }

        public StateRecord? MergeRecords(StateRecord previous, StateRecord current, StateRecord applied)
        {
            var previousRecord = previous as StateStateRecord<T>;
            var currentRecord = current as StateStateRecord<T>;
            var appliedRecord = applied as StateStateRecord<T>;
            if (Policy.Equivalent(currentRecord.Value, appliedRecord.Value))
                return current;
            else
            {
                var merged = Policy.merge(previousRecord.Value, currentRecord.Value, appliedRecord.Value);
                if (merged != null)
                {
                    return appliedRecord.Create().Also(it =>
                    {
                        (it as StateStateRecord<T>).Value = merged;
                    });
                }
                else
                {
                    return null;
                }
            }
        }


        private class StateStateRecord<T>(int snapshotId, T myValue) : StateRecord(snapshotId)
        {
            public T Value = myValue;
            
            public override void Assign(StateRecord value)
            {
#pragma warning disable CS8602 
                this.Value = (value as StateStateRecord<T>).Value;
#pragma warning restore CS8602 
            }

            public override StateRecord Create()
            {
                return new StateStateRecord<T>(Snapshot.Current().Id, Value);
            }

            internal R Overwritable<T, R>(IStateObject state, T candidate, Func<T, R> block) where T : StateRecord
            {
                var snapshot = Snapshot.Current();
                return Snapshot.Sync(() =>
                {
                    snapshot = Snapshot.Current();
                    var rec = OverwritableRecord(state, snapshot, candidate);
                    var res = block(rec as T);
                    return res;
                }).Also((v) =>
                {
                    Snapshot.NotifyWrite(snapshot, state);
                });

            }

            private StateRecord OverwritableRecord(IStateObject state, Snapshot snapshot, StateRecord candidate)
            {
                if (snapshot.ReadOnly)
                {
                    // If the snapshot is read-only, use the snapshot recordModified to report it.
                    snapshot.RecordModified(state);
                }
                var id = snapshot.Id;

                if (candidate.SnapshotId == id)
                    return candidate;

                var newData = Snapshot.Sync(() => NewOverwritableRecordLocked(state));
                newData.SnapshotId = id;
                snapshot.RecordModified(state);
                return newData;
            }

            private StateRecord NewOverwritableRecordLocked(IStateObject state)
            {
                var r = Snapshot.UsedLocked(state)?.Also((o) =>
                {
                    SnapshotId = int.MaxValue;
                });
                return r ?? Create(int.MaxValue).Also(o =>
                {
                    o.Next = state.FirstStateRecord;
                    state.PrependStateRecord(o);
                });
            }
        }
        public static implicit operator T(SnapshotMutableState<T> state) => state.Value;
    }
}
#pragma warning restore CS8602 // Dereference of a possibly null reference.


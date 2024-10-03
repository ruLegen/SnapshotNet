using SnapshotNet.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet
{
    public abstract class StateRecord
    {
        internal int SnapshotId { get; set; }

        protected StateRecord(int snapshotId)
        {
            SnapshotId = snapshotId;
        }

        internal StateRecord? Next { get; set; }

        abstract public void Assign(StateRecord value);
        abstract public StateRecord Create();

         public StateRecord Create(int snapshotID) => Create().Also(rec=> rec.SnapshotId = snapshotID);
    }
}

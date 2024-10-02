using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet
{
    public interface IStateObject
    {
        StateRecord FirstStateRecord { get; }

        void PrependStateRecord(StateRecord value);
        StateRecord? MergeRecords(StateRecord previous, StateRecord current, StateRecord applied);
    }   
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet
{
    public interface IState<T>
    {
        T Value { get; }
    }
}

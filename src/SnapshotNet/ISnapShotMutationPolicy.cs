using System;

namespace SnapshotNet
{
    public interface ISnapShotMutationPolicy<T>
    {
        bool Equivalent(T a, T b);
        T? merge(T previous, T current, T applied);
    }
}
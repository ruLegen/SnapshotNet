

using System;

namespace SnapshotNet
{
    public class NestedMutableSnapshot : MutableSnapshot
    {
        public override Snapshot Root => _parent;

        private readonly MutableSnapshot _parent;
        private bool deactivated = false;

        public NestedMutableSnapshot(int id, HashSet<int> invalidSet, Action<object>? readObserver = null, Action<object>? writeObserver = null, MutableSnapshot? parent = null) 
            : base(id, invalidSet, readObserver, writeObserver, parent)
        {
            _parent = parent;
            parent.ActivateNestedSnapshot();
        }

        public override void Dispose()
        {
            if (!disposed)
            {
                base.Dispose();
                Deactivate();
            }
        }
        private void Deactivate()
        {
            if (!deactivated)
            {
                deactivated = true;
                _parent.nestedDeactivated(this);
            }
        }
    }
}
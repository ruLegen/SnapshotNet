using SnapshotNet;
using System.Diagnostics;

namespace SnapshotNetSamples
{
    class Policy<T> : ISnapShotMutationPolicy<T>
    {
        public bool Equivalent(T a, T b)
        {
            return a.Equals(b);
        }

        public T? merge(T previous, T current, T applied)
        {
            return default;
        }
    }
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            var state = new SnapshotMutableState<int>(0,new Policy<int>());
            var s = Snapshot.TakeMutableSnapshot();
            var s1 = Snapshot.TakeMutableSnapshot();
            
            state.Value = 1;
            PrintState("Befor snap",state);
            s.Enter(() =>
            {
                state.Value = 2;
                PrintState("In snap 1", state);
                var r = Snapshot.TakeMutableSnapshot();
                r.Enter(() =>
                {
                    state.Value = 3;
                    PrintState("In snap2 ", state);
                    var rr = Snapshot.TakeMutableSnapshot();
                    int i = 0;
                });
                PrintState("after snap 1", state);

            });
            PrintState("after snap",state);
            Console.ReadLine();
        }

        static void PrintState<T>(string msg, SnapshotMutableState<T> state) => Console.WriteLine(msg +" " + state.ToString());
    }
}

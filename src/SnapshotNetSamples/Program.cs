using SnapshotNet;

namespace SnapshotNetSamples
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            var s = Snapshot.TakeMutableSnapshot();
            var s1 = Snapshot.TakeMutableSnapshot();
            s.Enter(()=> 
            {
                var r = Snapshot.TakeMutableSnapshot();
                r.Enter(() =>
                {
                    var rr = Snapshot.TakeMutableSnapshot();
                    int i = 0;
                });
            });

            Console.ReadLine();
        }
    }
}

using GTASaveData.Common;
using System.Collections.Generic;

namespace CarGenMerger
{
    public class CarGeneratorComparer :
        IComparer<ICarGenerator>,
        IEqualityComparer<ICarGenerator>
    {
        public int Compare(ICarGenerator x, ICarGenerator y)
        {
            double magX = x.Position.Magnitude;
            double magY = y.Position.Magnitude;

            if (magX < magY)
            {
                return -1;
            }
            else if (magX > magY)
            {
                return 1;
            }

            return 0;
        }

        public bool Equals(ICarGenerator x, ICarGenerator y)
        {
            return x.Model.Equals(y.Model)
                && x.Position.Equals(y.Position)
                && x.Heading.Equals(y.Heading)
                && x.AlarmChance.Equals(y.AlarmChance)
                && x.LockedChance.Equals(y.LockedChance)
                && x.Enabled.Equals(y.Enabled);
        }

        public int GetHashCode(ICarGenerator obj)
        {
            int hash = 17;
            hash += 23 * obj.Model.GetHashCode();
            hash += 23 * obj.Position.X.GetHashCode();
            hash += 23 * obj.Position.Y.GetHashCode();
            hash += 23 * obj.Position.Z.GetHashCode();
            hash += 23 * obj.Heading.GetHashCode();
            hash += 23 * obj.AlarmChance.GetHashCode();
            hash += 23 * obj.LockedChance.GetHashCode();
            hash += 23 * obj.Enabled.GetHashCode();

            return hash;
        }
    }
}

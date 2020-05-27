using GTASaveData.Types.Interfaces;
using System.Collections.Generic;

namespace CarGenMerger
{
    public class CarGeneratorComparer :
        IComparer<ICarGenerator>,
        IEqualityComparer<ICarGenerator>
    {
        public int Compare(ICarGenerator x, ICarGenerator y)
        {
            double magX = x.Position.GetMagnitude();
            double magY = y.Position.GetMagnitude();

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
                && x.Color1.Equals(y.Color1)
                && x.Color2.Equals(y.Color2)
                && x.Enabled.Equals(y.Enabled);
        }

        public int GetHashCode(ICarGenerator obj)
        {
            int hash = 17;
            hash += 23 * obj.Model.GetHashCode();
            hash += 23 * obj.Position.GetHashCode();
            hash += 23 * obj.Heading.GetHashCode();
            hash += 23 * obj.Color1.GetHashCode();
            hash += 23 * obj.Color2.GetHashCode();
            hash += 23 * obj.Enabled.GetHashCode();

            return hash;
        }
    }
}

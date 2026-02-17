using System;
using System.Collections.Generic;

namespace Sporefront.Models
{
    [System.Serializable]
    public struct HexCoordinate : IEquatable<HexCoordinate>
    {
        public int q; // column
        public int r; // row

        public HexCoordinate(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public int Distance(HexCoordinate other)
        {
            // Convert odd-r offset to cube coordinates
            int x1 = q - (r - (r & 1)) / 2;
            int z1 = r;
            int y1 = -x1 - z1;

            int x2 = other.q - (other.r - (other.r & 1)) / 2;
            int z2 = other.r;
            int y2 = -x2 - z2;

            return (Math.Abs(x1 - x2) + Math.Abs(y1 - y2) + Math.Abs(z1 - z2)) / 2;
        }

        public List<HexCoordinate> Neighbors()
        {
            int[] dq, dr;

            if (r % 2 == 0)
            {
                // Even rows
                dq = new int[] { 1, 0, -1, -1, -1, 0 };
                dr = new int[] { 0, 1, 1, 0, -1, -1 };
            }
            else
            {
                // Odd rows (shifted right)
                dq = new int[] { 1, 1, 0, -1, 0, 1 };
                dr = new int[] { 0, 1, 1, 0, -1, -1 };
            }

            var neighbors = new List<HexCoordinate>(6);
            for (int i = 0; i < 6; i++)
            {
                neighbors.Add(new HexCoordinate(q + dq[i], r + dr[i]));
            }
            return neighbors;
        }

        /// Direction order (clockwise from East): 0=East, 1=Southeast, 2=Southwest, 3=West, 4=Northwest, 5=Northeast
        public HexCoordinate Neighbor(int direction)
        {
            var allNeighbors = Neighbors();
            int normalizedDir = ((direction % 6) + 6) % 6;
            return allNeighbors[normalizedDir];
        }

        public List<HexCoordinate> CoordinatesInRing(int distance)
        {
            if (distance <= 0) return new List<HexCoordinate> { this };

            var results = new List<HexCoordinate>();
            var current = this;

            for (int i = 0; i < distance; i++)
                current = current.Neighbor(3); // West

            for (int direction = 0; direction < 6; direction++)
            {
                for (int i = 0; i < distance; i++)
                {
                    results.Add(current);
                    current = current.Neighbor(direction);
                }
            }

            return results;
        }

        public List<HexCoordinate> CoordinatesWithinRange(int range)
        {
            var results = new List<HexCoordinate> { this };
            for (int d = 1; d <= range; d++)
                results.AddRange(CoordinatesInRing(d));
            return results;
        }

        // IEquatable
        public bool Equals(HexCoordinate other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoordinate other && Equals(other);
        public override int GetHashCode() => q * 397 ^ r;
        public static bool operator ==(HexCoordinate a, HexCoordinate b) => a.Equals(b);
        public static bool operator !=(HexCoordinate a, HexCoordinate b) => !a.Equals(b);
        public override string ToString() => $"({q}, {r})";
    }
}

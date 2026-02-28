using System.Collections.Generic;
using NUnit.Framework;
using Sporefront.Models;

namespace Sporefront.Tests
{
    [TestFixture]
    public class HexCoordinateTests
    {
        // ================================================================
        // Construction & Equality
        // ================================================================

        [Test]
        public void Constructor_SetsQR()
        {
            var hex = new HexCoordinate(3, 5);
            Assert.AreEqual(3, hex.q);
            Assert.AreEqual(5, hex.r);
        }

        [Test]
        public void Equality_SameCoords_AreEqual()
        {
            var a = new HexCoordinate(2, 4);
            var b = new HexCoordinate(2, 4);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentCoords_AreNotEqual()
        {
            var a = new HexCoordinate(2, 4);
            var b = new HexCoordinate(3, 4);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void HashCode_WorksAsDictionaryKey()
        {
            var dict = new Dictionary<HexCoordinate, string>();
            var key = new HexCoordinate(5, 7);
            dict[key] = "hello";
            Assert.AreEqual("hello", dict[new HexCoordinate(5, 7)]);
        }

        // ================================================================
        // Distance
        // ================================================================

        [Test]
        public void Distance_SameHex_IsZero()
        {
            var hex = new HexCoordinate(3, 3);
            Assert.AreEqual(0, hex.Distance(hex));
        }

        [Test]
        public void Distance_AdjacentEvenRow_IsOne()
        {
            var hex = new HexCoordinate(3, 2); // even row
            var neighbor = new HexCoordinate(4, 2); // east neighbor
            Assert.AreEqual(1, hex.Distance(neighbor));
        }

        [Test]
        public void Distance_AdjacentOddRow_IsOne()
        {
            var hex = new HexCoordinate(3, 3); // odd row
            var neighbor = new HexCoordinate(4, 3); // east neighbor
            Assert.AreEqual(1, hex.Distance(neighbor));
        }

        [Test]
        public void Distance_IsSymmetric()
        {
            var a = new HexCoordinate(1, 2);
            var b = new HexCoordinate(5, 7);
            Assert.AreEqual(a.Distance(b), b.Distance(a));
        }

        [Test]
        public void Distance_KnownValue()
        {
            // Origin to (3,3) in odd-r offset
            var origin = new HexCoordinate(0, 0);
            var target = new HexCoordinate(3, 3);
            int d = origin.Distance(target);
            Assert.Greater(d, 0);
            // The distance should be at least max(|dq|, |dr|) = 3
            Assert.GreaterOrEqual(d, 3);
        }

        // ================================================================
        // Neighbors
        // ================================================================

        [Test]
        public void Neighbors_Returns6()
        {
            var hex = new HexCoordinate(3, 3);
            var neighbors = hex.Neighbors();
            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void Neighbors_AllDistanceOne()
        {
            var hex = new HexCoordinate(4, 4);
            foreach (var n in hex.Neighbors())
            {
                Assert.AreEqual(1, hex.Distance(n),
                    $"Neighbor {n} should be distance 1 from {hex}");
            }
        }

        [Test]
        public void Neighbors_EvenAndOddRowDiffer()
        {
            var even = new HexCoordinate(3, 2);
            var odd = new HexCoordinate(3, 3);
            var evenNeighbors = even.Neighbors();
            var oddNeighbors = odd.Neighbors();
            // They should not be identical sets
            bool allSame = true;
            for (int i = 0; i < 6; i++)
            {
                if (evenNeighbors[i] != oddNeighbors[i]) { allSame = false; break; }
            }
            Assert.IsFalse(allSame, "Even and odd row neighbors should differ");
        }

        // ================================================================
        // CoordinatesInRing
        // ================================================================

        [Test]
        public void CoordinatesInRing_Distance0_ReturnsSelf()
        {
            var hex = new HexCoordinate(3, 3);
            var ring = hex.CoordinatesInRing(0);
            Assert.AreEqual(1, ring.Count);
            Assert.AreEqual(hex, ring[0]);
        }

        [Test]
        public void CoordinatesInRing_Distance1_Returns6()
        {
            var hex = new HexCoordinate(5, 5);
            var ring = hex.CoordinatesInRing(1);
            Assert.AreEqual(6, ring.Count);
        }

        [Test]
        public void CoordinatesInRing_Distance2_Returns12()
        {
            var hex = new HexCoordinate(5, 5);
            var ring = hex.CoordinatesInRing(2);
            Assert.AreEqual(12, ring.Count);
        }

        [Test]
        public void CoordinatesInRing_AllAtCorrectDistance()
        {
            var hex = new HexCoordinate(5, 5);
            int distance = 3;
            var ring = hex.CoordinatesInRing(distance);
            foreach (var coord in ring)
            {
                Assert.AreEqual(distance, hex.Distance(coord),
                    $"Ring coord {coord} should be distance {distance} from {hex}");
            }
        }

        // ================================================================
        // CoordinatesWithinRange
        // ================================================================

        [Test]
        public void CoordinatesWithinRange_IncludesSelf()
        {
            var hex = new HexCoordinate(5, 5);
            var coords = hex.CoordinatesWithinRange(2);
            Assert.IsTrue(coords.Contains(hex));
        }

        [Test]
        public void CoordinatesWithinRange_CorrectCount()
        {
            // For hex grids: count within range R = 1 + 3*R*(R+1)
            var hex = new HexCoordinate(5, 5);
            var coords = hex.CoordinatesWithinRange(2);
            // Range 2: 1 + 6 + 12 = 19
            Assert.AreEqual(19, coords.Count);
        }

        // ================================================================
        // ToString
        // ================================================================

        [Test]
        public void ToString_FormatsCorrectly()
        {
            var hex = new HexCoordinate(3, 7);
            Assert.AreEqual("(3, 7)", hex.ToString());
        }
    }
}

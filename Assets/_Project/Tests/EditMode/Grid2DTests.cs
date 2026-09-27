using CozyFoodFactory.Grid;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class Grid2DTests
    {
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(0.99f, 0.99f, 0, 0)]
        [TestCase(1f, 1f, 1, 1)]
        [TestCase(2.8f, 3.2f, 2, 3)]
        [TestCase(-0.01f, -0.01f, -1, -1)]
        public void WorldToGrid_ReturnsContainingCell(float worldX, float worldY, int gridX, int gridY)
        {
            var grid = new Grid2D(1f);

            Vector2Int coordinate = grid.WorldToGrid(new Vector2(worldX, worldY));

            Assert.That(coordinate, Is.EqualTo(new Vector2Int(gridX, gridY)));
        }

        [Test]
        public void GridToWorld_ReturnsCellCenter()
        {
            var grid = new Grid2D(1f);

            Vector2 worldPosition = grid.GridToWorld(new Vector2Int(2, -3));

            Assert.That(worldPosition, Is.EqualTo(new Vector2(2.5f, -2.5f)));
        }

        [Test]
        public void WorldToGrid_UsesLowerCellBelowNegativeBoundary()
        {
            var grid = new Grid2D(1f);

            Assert.That(grid.WorldToGrid(new Vector2(-1f, 0f)), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(grid.WorldToGrid(new Vector2(-1.0001f, 0f)), Is.EqualTo(new Vector2Int(-2, 0)));
        }

        [Test]
        public void Conversions_SupportNonDefaultCellSizeAndOrigin()
        {
            var grid = new Grid2D(2.5f, new Vector2(10f, -5f));

            Assert.That(grid.WorldToGrid(new Vector2(14.99f, -0.01f)), Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(grid.GridToWorld(new Vector2Int(1, 1)), Is.EqualTo(new Vector2(13.75f, -1.25f)));
        }
    }
}

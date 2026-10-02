using NUnit.Framework;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Issue #18: any level fits any screen, letterboxed, with no scrolling (GDD §5 Camera).</summary>
    public class CameraFitTests
    {
        // The four screens named in the issue: 16:9 laptop, 1080p, 1440p and a 21:9 ultrawide.
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1366, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440)
        };

        // Wide (the GDD example), small, square and tall levels.
        private static readonly Vector2Int[] Levels =
        {
            new Vector2Int(32, 9), new Vector2Int(10, 5), new Vector2Int(16, 16), new Vector2Int(10, 20)
        };

        [Test]
        public void WholeLevelPlusMargin_IsVisible_OnEveryScreen(
            [ValueSource(nameof(Screens))] Vector2Int screen, [ValueSource(nameof(Levels))] Vector2Int level)
        {
            var aspect = CameraFit.ViewportAspect(screen.x, screen.y);
            var halfHeight = CameraFit.OrthographicSize(level.x, level.y, aspect);
            var halfWidth = halfHeight * aspect;

            Assert.GreaterOrEqual(halfHeight + 1e-4f, level.y * 0.5f + CameraFit.MarginCells, "level is cut off vertically");
            Assert.GreaterOrEqual(halfWidth + 1e-4f, level.x * 0.5f + CameraFit.MarginCells, "level is cut off horizontally");
        }

        [Test]
        public void Fit_IsTight_OneAxisExactlyFills_TheOtherGetsTheBars(
            [ValueSource(nameof(Screens))] Vector2Int screen, [ValueSource(nameof(Levels))] Vector2Int level)
        {
            var aspect = CameraFit.ViewportAspect(screen.x, screen.y);
            var halfHeight = CameraFit.OrthographicSize(level.x, level.y, aspect);
            var spareHeight = halfHeight - (level.y * 0.5f + CameraFit.MarginCells);
            var spareWidth = halfHeight * aspect - (level.x * 0.5f + CameraFit.MarginCells);

            Assert.AreEqual(0f, Mathf.Min(spareHeight, spareWidth), 1e-3f, "the level could be drawn larger");
        }

        [Test]
        public void WideLevel_IsLimitedByWidth_TallLevel_ByHeight()
        {
            var aspect = CameraFit.ViewportAspect(1920, 1080);
            Assert.AreEqual((32 * 0.5f + CameraFit.MarginCells) / aspect, CameraFit.OrthographicSize(32, 9, aspect), 1e-4f);
            Assert.AreEqual(20 * 0.5f + CameraFit.MarginCells, CameraFit.OrthographicSize(10, 20, aspect), 1e-4f);
        }

        [Test]
        public void Apply_RendersTheWholeScreen_AndPutsTheLevelInsideTheHudBand()
        {
            var root = new GameObject("Level").transform;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            try
            {
                root.position = new Vector3(3f, -2f, 0f);
                CameraFit.Apply(camera, root, 32, 9, Color.black);

                Assert.IsTrue(camera.orthographic);
                // Full screen, so the HUD strips are cleared every frame (an overlay HUD smears otherwise).
                Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), camera.rect);
                Assert.AreEqual(CameraFit.FullScreenSize(32, 9, camera.aspect), camera.orthographicSize, 1e-4f);

                var band = CameraFit.LevelViewport;
                var centre = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(16f, 4.5f, 0f)));
                Assert.AreEqual(0.5f, centre.x, 1e-4f);
                Assert.AreEqual(band.center.y, centre.y, 1e-4f, "level centre sits on the band centre");

                var margin = CameraFit.MarginCells;
                var bottomLeft = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(-margin, -margin, 0f)));
                var topRight = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(32f + margin, 9f + margin, 0f)));
                Assert.GreaterOrEqual(bottomLeft.x, -1e-4f);
                Assert.LessOrEqual(topRight.x, 1f + 1e-4f);
                Assert.GreaterOrEqual(bottomLeft.y, band.yMin - 1e-4f, "level stays above the bottom HUD strip");
                Assert.LessOrEqual(topRight.y, band.yMax + 1e-4f, "level stays below the top HUD strip");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}

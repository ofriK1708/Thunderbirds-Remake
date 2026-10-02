using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Sizes the orthographic camera so the whole level is visible on any screen (GDD §5 Camera). The level is
    /// centred and never scrolls; whichever axis has room to spare shows tomb-void bars (letterbox / pillarbox).
    /// </summary>
    public static class CameraFit
    {
        /// <summary>Empty cells kept around the level so the outer wall never touches the screen edge.</summary>
        public const float MarginCells = 0.75f;

        /// <summary>Screen area for the level: the strips above and below are left for the HUD.</summary>
        public static readonly Rect LevelViewport = new Rect(0f, 0.16f, 1f, 0.66f);

        /// <summary>
        /// The orthographic size (half the visible height, in cells) that fits a level of
        /// <paramref name="gridWidth"/> x <paramref name="gridHeight"/> cells into a view of the given
        /// <paramref name="aspect"/> (width / height): the tighter of "fit the height" and "fit the width".
        /// </summary>
        public static float OrthographicSize(int gridWidth, int gridHeight, float aspect, float margin = MarginCells)
        {
            var fitHeight = gridHeight * 0.5f + margin;
            var fitWidth = (gridWidth * 0.5f + margin) / aspect;
            return Mathf.Max(fitHeight, fitWidth);
        }

        /// <summary>Width / height of the level viewport on a screen of the given pixel size.</summary>
        public static float ViewportAspect(int screenWidth, int screenHeight) =>
            screenWidth * LevelViewport.width / (screenHeight * LevelViewport.height);

        /// <summary>Point the camera at the centre of a level whose cell (0, 0) is at <paramref name="levelRoot"/>.</summary>
        public static void Apply(Camera camera, Transform levelRoot, int gridWidth, int gridHeight, Color background)
        {
            camera.orthographic = true;
            camera.rect = LevelViewport;
            camera.backgroundColor = background;
            camera.transform.position = levelRoot.TransformPoint(new Vector3(gridWidth * 0.5f, gridHeight * 0.5f, -10f));
            camera.orthographicSize = OrthographicSize(gridWidth, gridHeight, camera.aspect);
        }
    }
}

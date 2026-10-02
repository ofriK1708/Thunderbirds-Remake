using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>A dock shows its ship's initial in a glowing ring: dim and pulsing while empty, bright when docked.</summary>
    public class DockMarkerTests
    {
        private static readonly Color Green = new Color(0.25f, 0.88f, 0.25f, 1f);

        private GameObject _root;
        private DockMarkerView _kestrel, _atlas;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Dock marker tests");
            _kestrel = DockMarkerView.Create(_root.transform, ShipId.Kestrel, Green);
            _atlas = DockMarkerView.Create(_root.transform, ShipId.Atlas, Green);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private static SpriteRenderer Part(DockMarkerView marker, string name) =>
            marker.transform.Find(name).GetComponent<SpriteRenderer>();

        [Test]
        public void EachShip_GetsItsOwnLetter_InsideARing()
        {
            Assert.AreEqual("Dock K", Part(_kestrel, "Letter").sprite.name);
            Assert.AreEqual("Dock A", Part(_atlas, "Letter").sprite.name);
            Assert.AreSame(Part(_kestrel, "Ring").sprite, Part(_atlas, "Ring").sprite, "one shared ring sprite");
            Assert.AreEqual("Docks", Part(_kestrel, "Ring").sortingLayerName);
            Assert.Greater(Part(_kestrel, "Letter").sortingOrder, Part(_kestrel, "Ring").sortingOrder);
        }

        [Test]
        public void Letters_AreDrawnTheRightWayUp()
        {
            // K: the top-left and top-right pixels are lit; A: the top corners are empty and the bar is solid.
            var k = Part(_kestrel, "Letter").sprite.texture;
            Assert.Greater(k.GetPixel(0, 6).a, 0.5f);
            Assert.Greater(k.GetPixel(4, 6).a, 0.5f);
            Assert.Less(k.GetPixel(2, 6).a, 0.5f);

            var a = Part(_atlas, "Letter").sprite.texture;
            Assert.Less(a.GetPixel(0, 6).a, 0.5f);
            Assert.Greater(a.GetPixel(2, 6).a, 0.5f);
            for (var x = 0; x < 5; x++) Assert.Greater(a.GetPixel(x, 3).a, 0.5f, "the A's crossbar");
        }

        [Test]
        public void Marker_FitsInsideTheTwoCellHighDock()
        {
            foreach (var marker in new[] { _kestrel, _atlas })
            foreach (var part in marker.GetComponentsInChildren<SpriteRenderer>())
            {
                var size = Vector2.Scale(part.sprite.bounds.size, part.transform.localScale);
                Assert.LessOrEqual(size.y, 2f, part.name);
                Assert.LessOrEqual(size.x, 2f, part.name);
            }
        }

        [Test]
        public void Place_CentresItOnTheDockFootprint()
        {
            _atlas.Hide();
            _atlas.Place(new GridPos(17, 1), 4, 2);
            Assert.IsTrue(_atlas.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(19f, 2f, 0f), _atlas.transform.localPosition);
        }

        [Test]
        public void Empty_ItPulsesDimly_Docked_ItIsBrightAndSteady()
        {
            float min = 1f, max = 0f;
            for (var i = 0; i < 100; i++)
            {
                _kestrel.Advance(0.05f);
                min = Mathf.Min(min, _kestrel.Brightness);
                max = Mathf.Max(max, _kestrel.Brightness);
            }
            Assert.Less(max, 0.8f, "never as bright as a docked marker");
            Assert.Greater(min, 0.4f, "never invisible");
            Assert.Greater(max - min, 0.2f, "visibly pulsing");

            _kestrel.SetLit(true);
            Assert.IsTrue(_kestrel.IsLit);
            Assert.AreEqual(1f, _kestrel.Brightness);
            _kestrel.Advance(0.7f);
            Assert.AreEqual(1f, _kestrel.Brightness, "steady while docked");

            _kestrel.SetLit(false);
            Assert.Less(_kestrel.Brightness, 0.8f);
        }
    }
}

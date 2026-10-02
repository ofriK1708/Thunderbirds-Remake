using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Thunderbirds.Tests.EditMode
{
    public class LevelOverlayTests
    {
        private LevelOverlayView _view;
        private int _restarts, _next, _resumes;
        [SetUp] public void SetUp()
        {
            _view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelOverlayView>("Assets/Resources/LevelOverlays.prefab"));
            _restarts = _next = _resumes = 0;
            _view.Bind(() => _resumes++, () => _restarts++, () => _next++, () => { }, () => { }, null);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_view.gameObject);
        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(b => b.name == name + " Button");

        [TestCase(FailReason.Crushed, "CRUSHED")]
        [TestCase(FailReason.OutOfOxygen, "OUT OF OXYGEN")]
        public void Failure_HoldsThenShowsReason_AndLocksButtonsForAnotherHalfSecond(FailReason reason, string title)
        {
            _view.ShowOutcome(false, reason, 0, false);
            Assert.IsFalse(Button("retry").gameObject.activeInHierarchy);
            Button("retry").onClick.Invoke(); Assert.AreEqual(0, _restarts);
            _view.Advance(0.49f);
            Assert.IsFalse(Button("retry").gameObject.activeInHierarchy);
            _view.Advance(0.02f);
            Assert.IsTrue(Button("retry").gameObject.activeInHierarchy);
            Assert.IsTrue(_view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == title));
            _view.Advance(0.49f); Button("retry").onClick.Invoke(); Assert.AreEqual(0, _restarts);
            _view.Advance(0.02f); Button("retry").onClick.Invoke(); Assert.AreEqual(1, _restarts);
        }

        [TestCase(true)] [TestCase(false)]
        public void Complete_ShowsOxygen_OnlyAllowsExistingNextLevel(bool hasNext)
        {
            _view.ShowOutcome(true, FailReason.OutOfOxygen, 42.3f, hasNext);
            _view.Advance(0.5f); _view.Advance(0.5f);
            Assert.IsTrue(_view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Oxygen remaining: 43 seconds"));
            Assert.AreEqual(hasNext, Button("next").interactable);
            Button("next").onClick.Invoke(); Assert.AreEqual(hasNext ? 1 : 0, _next);
        }

        [Test]
        public void Pause_HelpAndOptionsReturnToPause_HideCancelsPendingOutcome()
        {
            _view.ShowPause(); Assert.IsTrue(_view.IsReady);
            Button("help").onClick.Invoke();
            var help = _view.GetComponentInChildren<HowToPlayView>(true);
            Assert.IsTrue(help.gameObject.activeSelf); Assert.IsFalse(Button("resume").gameObject.activeInHierarchy);
            help.Close(); Assert.IsTrue(Button("resume").gameObject.activeInHierarchy);
            Button("options").onClick.Invoke();
            var options = _view.GetComponentInChildren<OptionsView>(true);
            Assert.IsTrue(options.gameObject.activeSelf);
            options.Close(); Button("resume").onClick.Invoke(); Assert.AreEqual(1, _resumes);
            _view.ShowOutcome(false, FailReason.Crushed, 0, false);
            _view.Hide(); _view.Advance(2);
            Assert.IsFalse(_view.IsVisible); Assert.IsFalse(Button("retry").gameObject.activeInHierarchy);
        }
    }
}

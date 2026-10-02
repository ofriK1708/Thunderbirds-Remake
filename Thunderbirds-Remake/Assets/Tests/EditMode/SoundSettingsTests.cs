using NUnit.Framework;
using TMPro;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// The real AudioManager and the Options sliders. LevelAudioTests cover which sound plays when, with a fake;
    /// these cover whether a played sound can be heard at all, and that the player's volume choice is kept.
    /// </summary>
    public class SoundSettingsTests
    {
        private float? _savedMusic, _savedEffects;

        [SetUp]
        public void SetUp()
        {
            _savedMusic = PlayerPrefs.HasKey(SoundSettings.MusicKey) ? PlayerPrefs.GetFloat(SoundSettings.MusicKey) : (float?)null;
            _savedEffects = PlayerPrefs.HasKey(SoundSettings.EffectsKey) ? PlayerPrefs.GetFloat(SoundSettings.EffectsKey) : (float?)null;
            PlayerPrefs.DeleteKey(SoundSettings.MusicKey);
            PlayerPrefs.DeleteKey(SoundSettings.EffectsKey);
            SoundSettings.Reload();
            AudioManager.ResetInstance();
        }

        [TearDown]
        public void TearDown()
        {
            AudioManager.ResetInstance();
            Restore(SoundSettings.MusicKey, _savedMusic);
            Restore(SoundSettings.EffectsKey, _savedEffects);
            PlayerPrefs.Save();
            SoundSettings.Reload();
        }

        private static void Restore(string key, float? value)
        {
            if (value.HasValue) PlayerPrefs.SetFloat(key, value.Value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static AudioSource Source(string name) =>
            AudioManager.Instance.transform.Find(name).GetComponent<AudioSource>();

        [Test]
        public void EffectsSource_IsAtFullVolume_SoOneShotsCanBeHeard()
        {
            // PlayOneShot multiplies its volume argument by the source volume: at 0, every effect was silent.
            Assert.AreEqual(1f, Source("Effects").volume);
            Assert.IsFalse(Source("Effects").loop);
            Assert.Greater(AudioManager.Instance.EffectsVolume, 0f);
        }

        [Test]
        public void EverySource_Is2D_AndLoopsOnlyWhereItShould()
        {
            foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>())
            {
                Assert.AreEqual(0f, source.spatialBlend, source.name);
                Assert.IsFalse(source.playOnAwake, source.name);
            }
            Assert.IsTrue(Source("Music").loop);
            Assert.IsTrue(Source("Overload").loop);
            Assert.IsTrue(Source("Kestrel engine").loop);
            Assert.IsTrue(Source("Atlas engine").loop);
            Assert.IsNotNull(Source("Atlas engine").clip);
            Assert.AreNotSame(Source("Kestrel engine").clip, Source("Atlas engine").clip);
        }

        [Test]
        public void Settings_DefaultTo75Percent_AreSaved_AndClamped()
        {
            Assert.AreEqual(SoundSettings.DefaultVolume, SoundSettings.Music);
            Assert.AreEqual(SoundSettings.DefaultVolume, SoundSettings.Effects);

            SoundSettings.SetMusic(0.2f);
            SoundSettings.SetEffects(7f);
            SoundSettings.Reload(); // as after restarting the game

            Assert.AreEqual(0.2f, SoundSettings.Music, 1e-5f);
            Assert.AreEqual(1f, SoundSettings.Effects, "clamped to 0..1");
        }

        [Test]
        public void PlayerVolume_ScalesTheDesignerVolume_AndZeroIsSilent()
        {
            var manager = AudioManager.Instance;
            SoundSettings.SetMusic(1f);
            SoundSettings.SetEffects(1f);
            var music = manager.MusicVolume;
            var effects = manager.EffectsVolume;
            var engine = manager.EngineVolume;
            Assert.Greater(music, 0f);
            Assert.Less(engine, effects, "engines sit below the effects");

            SoundSettings.SetMusic(0.5f);
            SoundSettings.SetEffects(0f);
            Assert.AreEqual(music * 0.5f, manager.MusicVolume, 1e-5f);
            Assert.AreEqual(0f, manager.EffectsVolume);
            Assert.AreEqual(0f, manager.EngineVolume, "the effects slider covers the engines too");
        }

        [Test]
        public void OptionsSliders_ShowTheSavedVolume_AndSaveWhenMoved()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/OptionsPanel.prefab");
            var panel = Object.Instantiate(prefab);
            try
            {
                var view = panel.GetComponent<OptionsView>();
                var serialized = new SerializedObject(view);
                var music = (Slider)serialized.FindProperty("music").objectReferenceValue;
                var effects = (Slider)serialized.FindProperty("effects").objectReferenceValue;
                var musicLabel = (TMP_Text)serialized.FindProperty("musicValue").objectReferenceValue;

                SoundSettings.SetMusic(0.3f);
                view.ShowSavedVolumes();
                Assert.AreEqual(30f, music.value, 1e-3f, "the prefab's sliders run 0..100");
                Assert.AreEqual(75f, effects.value, 1e-3f);
                Assert.AreEqual("30%", musicLabel.text);

                view.UpdateMusic(60f);
                view.UpdateEffects(10f);
                Assert.AreEqual(0.6f, SoundSettings.Music, 1e-4f);
                Assert.AreEqual(0.1f, SoundSettings.Effects, 1e-4f);
                Assert.AreEqual("60%", musicLabel.text);
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }
    }
}

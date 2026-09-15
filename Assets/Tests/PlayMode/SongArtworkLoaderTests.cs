#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SongArtworkLoaderTests
    {

        [UnityTest]
        public IEnumerator ObsoleteLoadsAndDisposedOwnersCannotPublishArtwork()
        {

            string token = Guid.NewGuid().ToString("N");
            string folder = "Assets/__SongArtworkTest_" + token;
            List<SongDefinition> songs = new();
            SongArtworkLoader loader = new();
            bool disposed = false;
            int changesAfterDispose = 0;
            loader.Changed += () => { if (disposed) changesAfterDispose++; };
            try
            {

                Directory.CreateDirectory(folder + "/Resources/" + token);
                for (int i = 0; i < 3; i++)
                {

                    string path = $"{folder}/Resources/{token}/{i}.png";
                    Texture2D pixels = new(4, 4);
                    pixels.SetPixels(new Color[16]);
                    pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    Object.Destroy(pixels);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                    SongDefinition song = ScriptableObject.CreateInstance<SongDefinition>();
                    JsonUtility.FromJsonOverwrite($"{{\"coverResourcePath\":\"{token}/{i}\"}}", song);
                    songs.Add(song);

                }

                loader.SetTargets(new[] { songs[0] });
                loader.SetTargets(new[] { songs[1] });
                double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                while (loader.Get(songs[1]) == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(loader.Get(songs[1]), Is.Not.Null);
                Assert.That(loader.Get(songs[0]), Is.Null);
                loader.SetTargets(new[] { songs[2] });
                disposed = true;
                loader.Dispose();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(loader.Get(songs[1]), Is.Null);
                Assert.That(loader.Get(songs[2]), Is.Null);
                Assert.That(changesAfterDispose, Is.Zero);

            }
            finally
            {

                loader.Dispose();
                foreach (SongDefinition song in songs) Object.Destroy(song);
                AssetDatabase.DeleteAsset(folder);

            }

        }

    }

}
#endif

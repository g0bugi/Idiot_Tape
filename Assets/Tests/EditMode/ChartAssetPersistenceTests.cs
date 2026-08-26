using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAssetPersistenceTests
    {

        private const string TemporaryChartPath =
            "Assets/Tests/EditMode/__ChartAssetPersistenceVerification.asset";

        [Test]
        public void DirtyChartAssetPersistsAfterSaveAndReload()
        {

            AssetDatabase.DeleteAsset(TemporaryChartPath);

            try
            {

                PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
                AssetDatabase.CreateAsset(chart, TemporaryChartPath);

                SerializedObject serializedChart = new(chart);
                SerializedProperty laneCount = serializedChart.FindProperty("laneCount");
                Assert.That(laneCount, Is.Not.Null);
                laneCount.intValue = 6;
                serializedChart.ApplyModifiedProperties();

                AssetDatabase.SaveAssetIfDirty(chart);
                AssetDatabase.ImportAsset(
                    TemporaryChartPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                PrototypeChart reloadedChart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(
                    TemporaryChartPath);
                Assert.That(reloadedChart, Is.Not.Null);
                Assert.That(reloadedChart.LaneCount, Is.EqualTo(6));

            }
            finally
            {

                AssetDatabase.DeleteAsset(TemporaryChartPath);

            }

        }

    }

}

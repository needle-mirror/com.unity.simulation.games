using System.Collections;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Simulation.Games.Tests
{
    public class GameSimManagerTests
    {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
        [UnityTest]
        public IEnumerator VerifyResetAndFlush_ResetsCounter_AndSavesCurrentStateTo_FileSystem()
        {
            GameSimManager.Instance.IncrementCounter("test", 1);
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 1);
            GameSimManager.Instance.ResetAndFlushCounterToDisk("test", FileAssert.Exists);
            yield return null;
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 0);
        }

        [UnityTest]
        public IEnumerator SetCounter_Sets_Counter()
        {
            GameSimManager.Instance.SetCounter("test", 3);
            yield return null;
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 3);
            GameSimManager.Instance.SetCounter("test", 7);
            yield return null;
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 7);
            GameSimManager.Instance.ResetCounter("test");
        }

        [UnityTest]
        public IEnumerator Reset_Resets_Counter()
        {
            GameSimManager.Instance.SetCounter("test", 3);
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 3);
            GameSimManager.Instance.ResetCounter("test");
            yield return null;
            Assert.IsTrue(GameSimManager.Instance.GetCounter("test").Value == 0);
        }

        [UnityTest]
        public IEnumerator Fetch_Config_Calls_Method()
        {
            bool ok = false;
            GameSimManager.Instance.FetchConfig(config =>
            {
                ok = true;
            });

            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsTrue(ok);
        }
#endif
    }
}

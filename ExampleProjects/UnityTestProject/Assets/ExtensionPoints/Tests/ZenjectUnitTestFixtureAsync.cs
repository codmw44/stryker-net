using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;

namespace Zenject
{
    public abstract class ZenjectUnitTestFixtureAsync
    {
        protected DiContainer Container { get; set; }

        public virtual UniTask Setup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Teardown()
        {
            return UniTask.CompletedTask;
        }

        [UnitySetUp]
        protected IEnumerator InternalSetup()
        {
            Container = new DiContainer(StaticContext.Container);
            return UniTask.ToCoroutine(
                async () => { await Setup(); });
        }

        [UnityTearDown]
        protected IEnumerator InternalTeardown()
        {
            return UniTask.ToCoroutine(
                async () =>
                {
                    await Teardown();
                    StaticContext.Clear();
                });
        }
    }
}
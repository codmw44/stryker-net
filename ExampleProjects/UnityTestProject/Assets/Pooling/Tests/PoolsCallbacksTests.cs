using System;
using Hypemasters.Pooling.Infrastructures;
using Hypemasters.Pooling.Installers;
using NUnit.Framework;
using FluentAssertions;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Common.Pooling.Tests
{
	[TestFixture]
	public class PoolsCallbacksTests : VContainerTestFixture
	{
		public class TestObj : MonoBehaviour, IPrefabPoolCallbacks
		{
			public bool SpawnInvoked = false;
			public bool ReturnInvoked = false;

			public void BeforeSpawn()
			{
				SpawnInvoked = true;
			}

			public void AfterReturnToPool()
			{
				ReturnInvoked = true;
			}
		}

		protected override void InstallDependencies(IContainerBuilder containerBuilder)
		{
			containerBuilder.RegisterGameObjectPrefabsPool();
			containerBuilder.RegisterDefaultPoolStrategy();
			containerBuilder.RegisterPool<TestObj>();
		}

		[Test]
		public void GetItem_Invoke()
		{
			var gm = new GameObject().AddComponent<TestObj>();

			var pool = Resolver.Resolve<IPrefabsPool<TestObj>>();
			var poolRef = pool.GetPoolReference(gm, 5);

			var item = poolRef.Get();

			item.Should().NotBeNull();

			item.SpawnInvoked.Should().BeTrue();
			poolRef.Dispose();
			Object.Destroy(gm.gameObject);
			Object.Destroy(item.gameObject);
		}

		[Test]
		public void ReturnItem_Invoke()
		{
			var gm = new GameObject().AddComponent<TestObj>();

			var pool = Resolver.Resolve<IPrefabsPool<TestObj>>();
			var poolRef = pool.GetPoolReference(gm, 5);

			var item = poolRef.Get();

			item.Should().NotBeNull();

			poolRef.Return(item);

			item.ReturnInvoked.Should().BeTrue();
			poolRef.Dispose();
			Object.Destroy(gm.gameObject);
			Object.Destroy(item.gameObject);
		}
	}
}

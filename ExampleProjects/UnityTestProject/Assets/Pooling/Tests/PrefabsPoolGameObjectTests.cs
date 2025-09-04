using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using FluentAssertions;
using Hypemasters.Pooling.Implementations;
using Hypemasters.Pooling.Infrastructures;
using Hypemasters.Pooling.Installers;
using Hypemasters.Pooling.Strategies;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using Assert = UnityEngine.Assertions.Assert;
using Object = UnityEngine.Object;

namespace Common.Pooling.Tests
{
	[TestFixture]
	public class PrefabsPoolGameObjectTests : VContainerTestFixture
	{
		public class EmptyMonoBehaviourForTest : MonoBehaviour
		{
		}

		protected override void InstallDependencies(IContainerBuilder containerBuilder)
		{
			containerBuilder.RegisterGameObjectPrefabsPool();
			containerBuilder.RegisterDefaultPoolStrategy();
		}

		[Test]
		public void GetItem_NotBeNull()
		{
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<GameObject>>();
			var poolRef = pool.GetPoolReference(gm, 5);

			var item = poolRef.Get();

			item.Should().NotBeNull();
			poolRef.Return(item);
			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void GetItem_TwoPrefabs_DifferentPools()
		{
			var gm = new GameObject("Test1");

			var pool = Resolver.Resolve<IPrefabsPool<GameObject>>();
			var poolRef = pool.GetPoolReference(gm, 5);

			var item = poolRef.Get();

			item.Should().NotBeNull();
			item.name.Should().Contain("Test1");
			
			var prefab2 = new GameObject("Test2");

			var poolRef2 = pool.GetPoolReference(prefab2, 3);
			var item2 = poolRef2.Get();

			item2.Should().NotBeNull();
			item2.name.Should().Contain("Test2");
			
			poolRef.Return(item);
			poolRef2.Return(item2);
			poolRef.Dispose();
			poolRef2.Dispose();
			Object.Destroy(gm);
			Object.Destroy(prefab2);
		}

	}
}

using System;
using System.Collections;
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
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace Common.Pooling.Tests
{
	[TestFixture]
	public class PrefabsPoolComponentTests : VContainerTestFixture
	{
		public class EmptyMonoBehaviourForTest : MonoBehaviour
		{

		}

		protected override void InstallDependencies(IContainerBuilder containerBuilder)
		{
			containerBuilder.RegisterGameObjectPrefabsPool();
			containerBuilder.RegisterDefaultPoolStrategy();		
			containerBuilder.RegisterPool<Transform>();
			containerBuilder.RegisterPool<EmptyMonoBehaviourForTest>();
		}

		[Test]
		public void GetItem_NotBeNull()
		{
			var parent = new GameObject();
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, 5);

			var item = poolRef.Get(parent.transform);

			item.Should().NotBeNull();
			poolRef.Return(item);
			poolRef.Dispose();
			Object.Destroy(parent);
			Object.Destroy(gm);
		}

		[Test]
		public void ReturnAlreadyContainedItem_NotBeAdded()
		{
			var poolSize = 5;

			var parent = new GameObject();
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, poolSize);

			var item = poolRef.Get(parent.transform);
			pool.FreeElementsInPool.Should().Be(poolSize - 1);

			poolRef.Return(item);
			poolRef.Return(item);

			pool.FreeElementsInPool.Should().Be(poolSize);

			poolRef.Dispose();
			Object.Destroy(parent);
			Object.Destroy(gm);
		}

		[Test]
		public void ReturnNull_NotAdded()
		{
			var count = 5;
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);

			poolRef.Return(null);

			pool.FreeElementsInPool.Should().Be(count);

			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void DestroyObjectAfterReturn_ItemsCreatedAndSizeNotChange()
		{
			var count = 1;
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);
			var item1 = poolRef.Get();
			poolRef.Return(item1);
			Object.Destroy(item1.gameObject);
			var item2 = poolRef.Get();

			UnityEngine.Assertions.Assert.IsNotNull(item2);

			pool.Size.Should().Be(count);
			pool.FreeElementsInPool.Should().Be(0);

			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void GetMoreItemsThanExist_ItemsCreatedAndSizeChange()
		{
			var poolSize = 5;
			var takenItemsCount = 10;
			var takedItems = new List<Transform>();

			var parent = new GameObject();
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, poolSize);

			for (var i = 0; i < takenItemsCount; ++i)
			{
				takedItems.Add(poolRef.Get(parent.transform));
			}

			foreach (var item in takedItems)
			{
				item.Should().NotBeNull();
			}

			pool.Size.Should().Be(takenItemsCount);
			pool.FreeElementsInPool.Should().Be(0);

			foreach (var t in takedItems)
			{
				poolRef.Return(t);
			}

			pool.Size.Should().Be(takenItemsCount);
			pool.FreeElementsInPool.Should().Be(takenItemsCount);
			takedItems.Clear();
			poolRef.Dispose();
			Object.Destroy(parent);
			Object.Destroy(gm);
		}

		[Test]
		public void GetRefAfterPoolDisposing_PoolInitializedCorrect()
		{
			var firstPoolSize = 5;
			var secondPoolSize = 2;

			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, firstPoolSize);
			poolRef.Dispose();

			poolRef = pool.GetPoolReference(gm.transform, secondPoolSize);
			pool.Size.Should().Be(secondPoolSize);
			pool.FreeElementsInPool.Should().Be(secondPoolSize);

			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void ReturnItemToTheFullPool_ItemNotAdded()
		{
			var gm = new GameObject();
			var item = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, PrefabsPoolGameObject.MaxSize);
			poolRef.Return(item.transform);

			pool.FreeElementsInPool.Should().Be(PrefabsPoolGameObject.MaxSize);

			poolRef.Dispose();
			Object.Destroy(gm);
			Object.Destroy(item);
		}

		[Test]
		public void PoolDisposing_ItemsDestroyed()
		{
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, 100);
			poolRef.Dispose();

			pool.Size.Should().Be(0);
			pool.FreeElementsInPool.Should().Be(0);

			Object.Destroy(gm);
		}

		[Test]
		public void OneOfFewPoolRefDisposed_PoolNotDisposed()
		{
			var poolsSize = 10;
			var parent = new GameObject();
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var firstPoolRef = pool.GetPoolReference(gm.transform, poolsSize);
			var secondPoolRef = pool.GetPoolReference(gm.transform, poolsSize);

			firstPoolRef.Dispose();

			var item = secondPoolRef.Get(parent.transform);
			item.Should().NotBeNull();
			secondPoolRef.Return(item);

			pool.Size.Should().Be(poolsSize);
			pool.FreeElementsInPool.Should().Be(poolsSize);

			secondPoolRef.Dispose();
			Object.Destroy(parent);
			Object.Destroy(gm);
		}

		[Test]
		public void OneOfRefDisposedTwoTimes_PoolNotDisposed()
		{
			var poolsSize = 10;
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var firstPoolRef = pool.GetPoolReference(gm.transform, poolsSize);
			var secondPoolRef = pool.GetPoolReference(gm.transform, poolsSize);

			firstPoolRef.Dispose();
			firstPoolRef.Dispose();

			pool.Size.Should().Be(poolsSize);
			pool.FreeElementsInPool.Should().Be(poolsSize);

			secondPoolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void DefaultStrategyPrewarm_AllObjectInRoot()
		{
			var count = 5;
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);

			pool.Root.gameObject.activeSelf.Should().Be(false);
			pool.Root.childCount.Should().Be(count);
			pool.Size.Should().Be(count);
			pool.FreeElementsInPool.Should().Be(count);

			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void DefaultStrategyGet_ObjectActiveInParent()
		{
			var count = 5;
			var parent = new GameObject().transform;
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();

			var poolRef = pool.GetPoolReference(gm.transform, count);
			var ins = poolRef.Get(parent);

			ins.parent.Should().Be(parent);
			ins.gameObject.activeSelf.Should().Be(true);

			poolRef.Return(ins);
			poolRef.Dispose();
			Object.Destroy(gm);
			Object.Destroy(parent.gameObject);
		}

		[Test]
		public void DefaultStrategyReturn_ObjectInRoot()
		{
			var count = 5;
			var parent = new GameObject().transform;
			var gm = new GameObject();
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);
			var ins = poolRef.Get(parent);
			poolRef.Return(ins);

			ins.parent.Should().Be(pool.Root);
			ins.gameObject.activeSelf.Should().Be(true);

			poolRef.Dispose();
			Object.Destroy(gm);
			Object.Destroy(parent.gameObject);
		}

		[Test]
		public void KeepParentStrategyPrewarm_AllObjectInRoot()
		{
			var count = 5;
			var gm = new GameObject("",typeof(KeepParentStrategy));
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);

			pool.Root.gameObject.activeSelf.Should().Be(false);
			pool.Root.childCount.Should().Be(count);
			pool.Size.Should().Be(count);
			pool.FreeElementsInPool.Should().Be(count);

			poolRef.Dispose();
			Object.Destroy(gm);
		}

		[Test]
		public void KeepParentStrategyGet_ObjectActiveInParent()
		{
			var count = 5;
			var parent = new GameObject().transform;
			var gm = new GameObject("",typeof(KeepParentStrategy));
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();

			var poolRef = pool.GetPoolReference(gm.transform, count);
			var ins = poolRef.Get(parent);

			ins.parent.Should().Be(parent);
			ins.gameObject.activeSelf.Should().Be(true);
			poolRef.Return(ins);
			poolRef.Dispose();
			Object.Destroy(gm);
			Object.Destroy(parent.gameObject);
		}

		[Test]
		public void KeepParentStrategyReturn_ObjectDeactiveInParent()
		{
			var count = 5;
			var parent = new GameObject().transform;
			var gm = new GameObject("",typeof(KeepParentStrategy));
			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, count);
			var ins = poolRef.Get(parent);
			poolRef.Return(ins);

			ins.parent.Should().Be(parent);
			ins.gameObject.activeSelf.Should().Be(false);

			poolRef.Dispose();
			Object.Destroy(gm);
			Object.Destroy(parent.gameObject);
		}

		[Test]
		public void GetItem_IncreaseCounterForInstantiatedObjectsInPoolRef()
		{
			var parent = new GameObject();
			var gm = new GameObject();

			var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
			var poolRef = pool.GetPoolReference(gm.transform, 5);

			((PrefabsPoolComponent<Transform>.PrefabPoolComponentReference<Transform>)poolRef).PoolInstantiatedObjects.Count.Should().Be(0);
			var item = poolRef.Get(parent.transform);
			((PrefabsPoolComponent<Transform>.PrefabPoolComponentReference<Transform>)poolRef).PoolInstantiatedObjects.Count.Should().Be(1);

			item.Should().NotBeNull();
			poolRef.Return(item);
			((PrefabsPoolComponent<Transform>.PrefabPoolComponentReference<Transform>)poolRef).PoolInstantiatedObjects.Count.Should().Be(0);
			poolRef.Dispose();

			Object.Destroy(parent);
			Object.Destroy(gm);
		}

		[UnityTest] [Ignore("")]
		public IEnumerator PoolDispose_ItemsWithNoRefsAlsoDispose()
		{
			return UniTask.ToCoroutine(async ()=>
			{
				var countForInstantiate = 5;
				var parent = new GameObject();
				var gm = new GameObject();

				var pool = Resolver.Resolve<IPrefabsPool<Transform>>();
				var poolRef = pool.GetPoolReference(gm.transform, 5);

				var poolInstantiatedObjects = ((PrefabsPoolComponent<Transform>.PrefabPoolComponentReference<Transform>)poolRef).PoolInstantiatedObjects;
				var isDisposed = (bool)GetPrivateFieldFromPrefabPoolReference(poolRef,"_isDisposed");

				poolInstantiatedObjects.Count.Should().Be(0);
				isDisposed.Should().BeFalse();

				for (var i = 0; i < countForInstantiate; i++)
				{
					poolRef.Get(parent.transform);
				}

				poolInstantiatedObjects.Count.Should().Be(5);

				var items = new Transform[poolInstantiatedObjects.Count];

				int index = 0;
				foreach (var pooledObject in poolInstantiatedObjects)
				{
					items[index] = pooledObject;
					index++;
				}

				poolRef.Dispose();

				poolInstantiatedObjects.Count.Should().Be(0);

				await UniTask.WaitForEndOfFrame();

				for (var i = 0; i < items.Length; i++)
				{
					FluentActions.Invoking( () => items[i].gameObject).Should().Throw<MissingReferenceException>();
				}

				isDisposed = (bool)GetPrivateFieldFromPrefabPoolReference(poolRef,"_isDisposed");
				isDisposed.Should().BeTrue();
				pool.FreeElementsInPool.Should().Be(0);

				Object.Destroy(parent);
				Object.Destroy(gm);
			});
			
		}

		private static object GetPrivateFieldFromPrefabPoolReference<T>(
			IPoolReference<T> poolReference,
			string fieldName) where T : Component
		{
			var poolInstantiatedObjects = typeof(PrefabsPoolComponent<T>.PrefabPoolComponentReference<T>)
				.GetField(fieldName,
				BindingFlags.NonPublic |
				BindingFlags.Instance)
				?.GetValue(poolReference);

			return poolInstantiatedObjects;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Package.Pooling.Infrastructures;
using UnityEngine;

namespace Package.Pooling.Implementations
{
	[SuppressMessage("ControllersTree", "RA4:You should use ZLogger")]
	public sealed partial class PrefabsPoolGameObject
	{
		public sealed class PrefabPoolGameObjectReference : IDisposable, IPoolReference<GameObject>
		{
			private readonly PrefabsPoolGameObject _prefabPoolGameObject;
			private readonly HashSet<GameObject> _poolInstantiatedObjects;
			private bool _isDisposed;

			internal HashSet<GameObject> PoolInstantiatedObjects => _poolInstantiatedObjects;

			public PrefabPoolGameObjectReference(PrefabsPoolGameObject prefabPoolGameObject)
			{
				_prefabPoolGameObject = prefabPoolGameObject;
				_poolInstantiatedObjects = new HashSet<GameObject>();
			}

			public GameObject Get(Transform parent = null)
			{
				if (_isDisposed)
				{
					throw new ObjectDisposedException(nameof(PrefabPoolGameObjectReference), "Already disposed");
				}

				var instantiatedObject = _prefabPoolGameObject.Get(parent);
				_poolInstantiatedObjects.Add(instantiatedObject);

				return instantiatedObject;
			}

			public void Return(GameObject pooledObject)
			{
				if (_isDisposed)
				{
					throw new ObjectDisposedException(nameof(PrefabPoolGameObjectReference), "Already disposed");
				}

				if (pooledObject != null)
				{
					if (!_poolInstantiatedObjects.Contains(pooledObject))
					{
						Debug.LogWarning($"Object {pooledObject.name} doesn't exist in _poolInstantiatedObjects list");
					}
					else
					{
						_poolInstantiatedObjects.Remove(pooledObject);
					}
				}

				_prefabPoolGameObject.Return(pooledObject);
			}

			public void Dispose()
			{
				if(_isDisposed)
					return;

				foreach (var pooledObject in _poolInstantiatedObjects)
				{
					if (pooledObject == null)
						continue;

					_prefabPoolGameObject.Return(pooledObject);
				}

				_poolInstantiatedObjects.Clear();
				_prefabPoolGameObject.PoolReferenceDisposed();
				_isDisposed = true;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hypemasters.Pooling.Infrastructures
{
	public interface IPoolReference<T> : IDisposable
	{
		T Get(Transform parent = null);
		void Return(T pooledObject);
	}
}
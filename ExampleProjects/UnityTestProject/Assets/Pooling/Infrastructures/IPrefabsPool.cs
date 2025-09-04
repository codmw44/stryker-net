using UnityEngine;

namespace Hypemasters.Pooling.Infrastructures
{
	public interface IPrefabsPool<T> where T : Object
	{
		int FreeElementsInPool { get; }
		Transform Root { get; }
		int Size { get; }
		IPoolReference<T> GetPoolReference(T prefab, int size = 0);
	}
}

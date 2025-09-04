using UnityEngine;

namespace Hypemasters.Pooling.Infrastructures
{
	public interface ICreatePoolStrategy
	{
		GameObject CreateInstance(GameObject prefab, Transform root);
	}
}

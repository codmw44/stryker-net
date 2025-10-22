using UnityEngine;

namespace Package.Pooling.Strategies
{
	public sealed class DefaultPoolStrategy : DefaultPoolStrategyBase
	{
		protected override GameObject GetPrefabInstance<T>(T prefab, Transform root)
		{
			var instantiate = Object.Instantiate(prefab, root);
			if (instantiate is GameObject go)
			{
				return go;
			}
			else if (instantiate is Component component)
			{
				return component.gameObject;
			}

			Debug.LogError($"Trying to instatiate {typeof(T)} but supports only GameObject and Components");
			return null;
		}
	}
}

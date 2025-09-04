using UnityEngine;

namespace Hypemasters.Pooling.Infrastructures
{
	public interface IReturnPoolStrategy
	{
		void ProcessBeforeReturn(GameObject instance, Transform root);
	}
}

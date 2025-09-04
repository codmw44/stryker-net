using UnityEngine;

namespace Hypemasters.Pooling.Infrastructures
{
    public interface IGetPoolStrategy
    {
        void ProcessBeforeGet(GameObject instance, Transform parent);
    }
}
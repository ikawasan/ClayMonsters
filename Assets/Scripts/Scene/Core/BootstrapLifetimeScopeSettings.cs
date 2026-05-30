using UnityEngine;

namespace Scene.Core
{
    [CreateAssetMenu(fileName = "BootstrapLifetimeScopeSettings", menuName = "Scriptable Objects/BootstrapLifetimeScopeSettings")]
    public class BootstrapLifetimeScopeSettings : ScriptableObject
    {
        [SerializeField] ClayMonstersLifetimeScope clayMonstersLifetimeScopePrefab;

        public ClayMonstersLifetimeScope ClayMonstersLifetimeScopePrefab => clayMonstersLifetimeScopePrefab;
    }
}
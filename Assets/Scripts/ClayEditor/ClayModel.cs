using UnityEngine;

namespace ClayEditor
{
    public enum ModelType
    {
        Human,
        Wolf,
        Crow
    }

    public class ClayModel : MonoBehaviour
    {
        [SerializeField] private ModelType modelType;

        [SerializeField] private Animator animator;

        [SerializeField] private Transform boneRoot;

        public ModelType ModelType => modelType;
        public Animator Animator => animator;
        public Transform BoneRoot => boneRoot;
    }
}

using UnityEngine;

namespace ClayEditor
{
    public class ClayModel : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [SerializeField] private Transform boneRoot;

        public Animator Animator => animator;
        public Transform BoneRoot => boneRoot;
    }
}

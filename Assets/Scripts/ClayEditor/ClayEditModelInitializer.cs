using ClayEditor.Interface;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClayEditor
{
    /// <summary>
    /// シーン起動時に固定の編集対象モデルを一度だけ設定
    /// </summary>
    public class ClayEditModelInitializer : MonoBehaviour, IInitializable
    {
        [Inject] private readonly IClaySceneContext context;

        [Tooltip("編集対象となるClayModel")]
        [SerializeField] private ClayModel clayModel;

        /// <inheritdoc />
        public void Initialize()
        {
            if (clayModel == null)
            {
                Debug.LogWarning("[ClayEditModelInitializer] clayModel が未設定です。");
                return;
            }

            context.SetModel(clayModel);
        }
    }
}
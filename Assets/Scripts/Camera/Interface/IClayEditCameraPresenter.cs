using UnityEngine;

namespace Camera.Interface
{
    public interface IClayEditCameraPresenter
    {
        /// <summary>
        /// シーン生成時の初期化処理
        /// </summary>
        void Setup();

        /// <summary>
        /// シーンに入った時の処理
        /// </summary>
        void OnEnter();

        /// <summary>
        /// シーンから抜ける時の処理
        /// </summary>
        void OnExit();
    }
}

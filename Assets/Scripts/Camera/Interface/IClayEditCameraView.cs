using UnityEngine;

namespace Camera.Interface
{
    public interface IClayEditCameraView
    {

        /// <summary>
        /// カメラ操作の有効・無効を切り替える
        /// </summary>
        void SetCameraEnable(bool isEnable);

        /// <summary>
        /// 周回の中心となる注視点を設定
        /// </summary>
        void SetFocusPosition(Vector3 centerPosition);

        /// <summary>
        /// シーン入場時のカメラ位置を確定する
        /// </summary>
        void SetInitializeView();
    }
}

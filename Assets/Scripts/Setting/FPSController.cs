using UnityEngine;

namespace Setting
{
    /// <summary>
    /// FPS設定クラス
    /// </summary>
    public class FPSController : MonoBehaviour
    {
        [Tooltip("固定したいフレームレートを指定します")]
        [SerializeField] private int targetFPS = 60;

        void Start()
        {
            // 1. VSync（垂直同期）をオフにする
            // ※これを0にしないとモニターのリフレッシュレートが優先されてしまう
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFPS;
        }

        public void ChangeFPS(int newFPS)
        {
            targetFPS = newFPS;
            Application.targetFrameRate = targetFPS;
        }
    }
}

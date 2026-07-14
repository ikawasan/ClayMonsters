using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 敵モデル保存が利用可能かを判定する
    /// Unityエディタ上でのみ敵保存を許可する
    /// </summary>
    public static class EnemySaveAvailability
    {
        /// <summary>
        /// 敵としての保存操作が利用可能か
        /// </summary>
        public static bool IsAvailable => Application.isEditor;
    }
}

namespace SaveData
{
    /// <summary>
    /// 敵モデル保存が利用可能かを判定する
    /// EditorまたはCLAY_ENABLE_ENEMY_SAVE付きBuild Profileでのみ許可する
    /// </summary>
    public static class EnemySaveAvailability
    {
        /// <summary>
        /// 敵としての保存操作が利用可能か
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || CLAY_ENABLE_ENEMY_SAVE
                return true;
#else
                return false;
#endif
            }
        }
    }
}

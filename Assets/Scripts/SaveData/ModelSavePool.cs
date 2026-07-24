namespace SaveData
{
    /// <summary>
    /// モデルセーブデータの保存先プール
    /// </summary>
    public enum ModelSavePool
    {
        /// <summary>
        /// 造形直後の未育成モデル
        /// </summary>
        Player,

        /// <summary>
        /// 育成完了後に戦闘で使用するモデル
        /// </summary>
        TrainedPlayer,

        /// <summary>
        /// ClayEditで作成しBattleNpcの敵として使うモデル
        /// </summary>
        Enemy
    }

    /// <summary>
    /// プールごとのファイル名とスロット数
    /// </summary>
    public static class ModelSavePoolSettings
    {
        /// <summary>
        /// 未育成・敵プールのスロット数
        /// </summary>
        public const int SlotCount = 10;

        /// <summary>
        /// 育成済みプールのスロット数(5列×10行)
        /// </summary>
        public const int TrainedSlotCount = 50;

        /// <summary>
        /// 育成済み一覧の列数
        /// </summary>
        public const int TrainedGridColumnCount = 5;

        /// <summary>
        /// 育成済み一覧の行数
        /// </summary>
        public const int TrainedGridRowCount = 10;

        /// <summary>
        /// プールごとのスロット数を返す
        /// </summary>
        /// <param name="pool">セーブプール</param>
        public static int GetSlotCount(ModelSavePool pool)
        {
            return pool == ModelSavePool.TrainedPlayer
                ? TrainedSlotCount
                : SlotCount;
        }

        /// <summary>
        /// スロット番号が有効か返す
        /// </summary>
        /// <param name="pool">セーブプール</param>
        /// <param name="slotIndex">スロット番号</param>
        public static bool IsValidSlotIndex(ModelSavePool pool, int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < GetSlotCount(pool);
        }

        /// <summary>
        /// メタデータJSONのファイル名を返す
        /// </summary>
        public static string GetMetadataFileName(ModelSavePool pool)
        {
            return pool switch
            {
                ModelSavePool.Player => "ClayModelSave.json",
                ModelSavePool.TrainedPlayer => "ClayModelTrainedSave.json",
                ModelSavePool.Enemy => "EnemyModelSave.json",
                _ => "ClayModelSave.json"
            };
        }

        /// <summary>
        /// glbファイル名を返す
        /// </summary>
        public static string GetGlbFileName(ModelSavePool pool, int slotIndex)
        {
            string prefix = GetFilePrefix(pool);
            return $"{prefix}_Slot{slotIndex}.glb";
        }

        /// <summary>
        /// サムネイルPNGのファイル名を返す
        /// </summary>
        public static string GetThumbnailFileName(ModelSavePool pool, int slotIndex)
        {
            string prefix = GetFilePrefix(pool);
            return $"{prefix}_Slot{slotIndex}.png";
        }

        /// <summary>
        /// 造形ボクセルスナップショットのファイル名を返す
        /// </summary>
        public static string GetVoxelFileName(ModelSavePool pool, int slotIndex)
        {
            string prefix = GetFilePrefix(pool);
            return $"{prefix}_Slot{slotIndex}.voxel";
        }

        private static string GetFilePrefix(ModelSavePool pool)
        {
            return pool switch
            {
                ModelSavePool.Player => "ClayModel",
                ModelSavePool.TrainedPlayer => "ClayModelTrained",
                ModelSavePool.Enemy => "EnemyModel",
                _ => "ClayModel"
            };
        }
    }
}

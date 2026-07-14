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
        /// 各プールのスロット数
        /// </summary>
        public const int SlotCount = 10;

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

using System.Collections.Generic;
using System.Threading;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SaveData.Interface
{
    /// <summary>
    /// モデルのセーブ・ロード（スロット管理）を行うサービスのインターフェース
    /// </summary>
    public interface IClayModelSaveService
    {
        /// <summary>
        /// 指定プールのスロットへモデルを保存する
        /// glbを出力し、骨格解析で選んだ攻撃を保存し、メタデータをJSONへ書き込む
        /// </summary>
        /// <param name="pool">保存先プール</param>
        /// <param name="slotIndex">保存先スロット番号</param>
        /// <param name="modelName">プレイヤーが付けた名前</param>
        /// <param name="status">モデルのステータス</param>
        /// <param name="attackMotions">骨格解析で選んだ保存登録用の攻撃(4件)</param>
        /// <param name="runtimeRenderer">出力するスキンメッシュ</param>
        /// <param name="boneRoot">ボーン階層のルート</param>
        /// <param name="thumbnailPng">サムネイル画像のPNGバイト列(nullなら保存しない)</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>保存に成功したか</returns>
        UniTask<bool> SaveAsync(
            ModelSavePool pool,
            int slotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            byte[] thumbnailPng,
            CancellationToken cancellationToken);

        /// <summary>
        /// 指定プールの全スロットのセーブデータを読み込む
        /// ファイルが無い場合は空のスロットデータを返す
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <returns>全スロットのセーブデータ</returns>
        ClayModelSaveData Load(ModelSavePool pool);

        /// <summary>
        /// 指定プールのスロットデータを取得する。未使用や範囲外ならnull
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <returns>スロットのデータ、無ければnull</returns>
        ModelSaveSlot GetSlot(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 指定プールのスロットのサムネイル画像を読み込んでTexture2Dとして返す。無ければnull
        /// 戻り値のTexture2Dは呼び出し側が不要になったらDestroyすること
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <returns>サムネイルのTexture2D、無ければnull</returns>
        Texture2D LoadThumbnail(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 指定プールのスロットの保存データとglb・サムネイルファイルを削除する
        /// Playerプールの場合は同番号のTrainedPlayerスロットも削除する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        void DeleteSlot(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 指定プールにロード可能なモデルセーブデータが1件以上あるか
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <returns>使用済みスロットにglbファイルが存在するか</returns>
        bool HasAnySavedModel(ModelSavePool pool);

        /// <summary>
        /// 指定プールのスロットのステータスだけを更新する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <param name="status">更新後ステータス</param>
        /// <returns>更新に成功したか</returns>
        bool UpdateSlotStatus(ModelSavePool pool, int slotIndex, ModelStatus status);

        /// <summary>
        /// 指定プールのスロットの攻撃構成だけを更新する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <param name="attackMotions">更新後攻撃</param>
        /// <returns>更新に成功したか</returns>
        bool UpdateSlotAttackMotions(ModelSavePool pool, int slotIndex, IReadOnlyList<MotionType> attackMotions);

        /// <summary>
        /// 指定プールのスロットの育成途中データを取得する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <returns>育成途中データ</returns>
        TrainingSlotProgress GetTrainingProgress(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 指定プールのスロットの育成途中データを保存する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>保存に成功したか</returns>
        bool SaveTrainingProgress(ModelSavePool pool, int slotIndex, TrainingSlotProgress progress);

        /// <summary>
        /// 指定プールのスロットの育成途中データを削除する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        void ClearTrainingProgress(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 指定プールのスロットに育成途中データがあるか
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <returns>育成途中ならtrue</returns>
        bool HasTrainingProgress(ModelSavePool pool, int slotIndex);

        /// <summary>
        /// 未育成スロットから育成後スロットへ結果を保存する
        /// </summary>
        /// <param name="trainedSlotIndex">育成済みスロット番号</param>
        /// <param name="playerSlotIndex">未育成スロット番号</param>
        /// <param name="modelName">モデル名</param>
        /// <param name="status">育成後ステータス</param>
        /// <param name="attackMotions">育成後攻撃構成</param>
        /// <returns>保存に成功したか</returns>
        bool SaveTrainedResultFromPlayerSlot(
            int trainedSlotIndex,
            int playerSlotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions);

        /// <summary>
        /// 育成完了結果を育成済みスロットへ保存する
        /// 表示中モデルがあればglbを再出力し無ければ未育成glbを複製する
        /// </summary>
        /// <param name="trainedSlotIndex">育成済みスロット番号</param>
        /// <param name="playerSlotIndex">未育成スロット番号</param>
        /// <param name="modelName">モデル名</param>
        /// <param name="status">育成後ステータス</param>
        /// <param name="attackMotions">育成後攻撃構成</param>
        /// <param name="runtimeRenderer">表示中メッシュ</param>
        /// <param name="boneRoot">ボーン階層ルート</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>保存に成功したか</returns>
        UniTask<bool> SaveTrainedResultAsync(
            int trainedSlotIndex,
            int playerSlotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            CancellationToken cancellationToken);
    }
}

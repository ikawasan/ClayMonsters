using R3;
using SaveData;
using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 戦闘前のモンスター選択UIの抽象。選択スロットと読み込み完了を提供する。
    /// </summary>
    public interface IMonsterSelection
    {
        /// <summary>
        /// 一覧表示に使うセーブプール
        /// </summary>
        ModelSavePool SavePool { get; }

        /// <summary>
        /// 選択スロットの表示(名前・サムネイル)を最新のセーブ内容で更新する。
        /// </summary>
        void Refresh();

        /// <summary>
        /// 戦闘フローが選択待ちに入る前にスロット一覧だけ整える
        /// </summary>
        void PrepareForSelectionWait();

        /// <summary>
        /// 選択UIの入力と表示を有効化する
        /// </summary>
        void EnsureSelectionInputEnabled();

        /// <summary>
        /// モデルのロードが完了したときに発火する(読み込んだモデルのルート)。
        /// </summary>
        Observable<GameObject> OnModelLoaded { get; }

        /// <summary>
        /// 読み込み済みモデル。未ロードはnull
        /// </summary>
        GameObject LoadedModel { get; }

        /// <summary>
        /// 直近にロードしたスロット番号(未ロードは-1)。
        /// </summary>
        int SelectedSlotIndex { get; }

        /// <summary>
        /// 参加者構築失敗後に選択UIへ戻す
        /// </summary>
        void RestoreAfterParticipantFailure();
    }
}
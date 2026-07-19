using Cysharp.Threading.Tasks;
using SaveData;
using System.Threading;
using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// モンスター選択UIの表示からロード完了までを一括管理する
    /// </summary>
    public interface IMonsterSelectionSession
    {
        /// <summary>
        /// 一覧表示に使うセーブプール
        /// </summary>
        ModelSavePool SavePool { get; }

        /// <summary>
        /// 直近にロードしたスロット番号(未ロードは-1)
        /// </summary>
        int SelectedSlotIndex { get; }

        /// <summary>
        /// シーン入場時の準備(レイアウト整備と非表示)
        /// </summary>
        void PrepareEntry();

        /// <summary>
        /// 選択UIを表示してモデルロード完了まで待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask<GameObject> WaitForModelAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 選択UIを非表示にする
        /// </summary>
        void Hide();

        /// <summary>
        /// シーン退場時に選択UIと保持モデルを破棄する
        /// </summary>
        void HideForLeave();

        /// <summary>
        /// 参加者構築失敗後に選択UIへ戻す
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask RestoreAfterParticipantFailureAsync(CancellationToken cancellationToken);
    }

}

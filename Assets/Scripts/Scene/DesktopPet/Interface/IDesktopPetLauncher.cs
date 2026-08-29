using System.Collections.Generic;

namespace Scene.DesktopPet.Interface
{
    /// <summary>
    /// デスクトップペットモードを開始する
    /// </summary>
    public interface IDesktopPetLauncher
    {
        /// <summary>
        /// 指定スロットのペットをデスクトップへ出す
        /// </summary>
        /// <param name="playerSlotIndices">未育成スロット番号(最大5)</param>
        /// <param name="stayOnTop">最前面ならtrue最背面ならfalse</param>
        /// <returns>起動または引き継ぎ開始ならtrue</returns>
        bool Launch(IReadOnlyList<int> playerSlotIndices, bool stayOnTop);
    }
}

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
        void Launch(IReadOnlyList<int> playerSlotIndices);
    }
}

using Scene.DesktopPet.Interface;

namespace Scene.DesktopPet
{
    /// <summary>
    /// 実行環境向けのペット窓実装を返す
    /// </summary>
    public static class DesktopPetWindowFactory
    {
        /// <summary>
        /// プラットフォームに合わせた窓制御を生成する
        /// </summary>
        /// <param name="stayOnTop">最前面ならtrue最背面ならfalse</param>
        public static IDesktopPetWindow Create(bool stayOnTop)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return new MacDesktopPetWindow(stayOnTop);
#else
            return new WindowsDesktopPetWindow(stayOnTop);
#endif
        }
    }
}

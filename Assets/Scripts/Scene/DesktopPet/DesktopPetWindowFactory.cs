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
        public static IDesktopPetWindow Create()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return new MacDesktopPetWindow();
#else
            return new WindowsDesktopPetWindow();
#endif
        }
    }
}

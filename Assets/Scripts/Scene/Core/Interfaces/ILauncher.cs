using Cysharp.Threading.Tasks;

namespace Scene.Core
{
    public interface ILauncher
    {
        UniTask Launch();
        void Reboot();
    }
}

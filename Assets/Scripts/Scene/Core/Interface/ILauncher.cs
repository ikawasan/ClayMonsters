using Cysharp.Threading.Tasks;

namespace Scene.Core.Interface
{
    public interface ILauncher
    {
        UniTask Launch();
        void Reboot();
    }
}

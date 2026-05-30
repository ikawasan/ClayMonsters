using Cysharp.Threading.Tasks;
using Lighthouse.Scene;

namespace Scene.Core.Interfaces
{
    public interface IClayMonsterSceneManager
    {
        bool IsTransition { get; }

        UniTask TransitionScene(
            TransitionDataBase nextTransitionData,
            TransitionType transitionType = TransitionType.Auto,
            MainSceneId backMainSceneId = default);

        UniTask BackScene(TransitionType transitionType = TransitionType.Auto);

        UniTask PreReboot();
    }
}

using Cysharp.Threading.Tasks;
using Lighthouse.Scene;

namespace Scene.Core.Interface
{
    public interface IClayMonsterSceneManager
    {
        bool IsTransition { get; }

        UniTask TransitionScene(
            TransitionDataBase nextTransitionData,
            TransitionType transitionType = TransitionType.Exclusive,
            MainSceneId backMainSceneId = default);

        UniTask BackScene(TransitionType transitionType = TransitionType.Exclusive);

        UniTask PreReboot();
    }
}

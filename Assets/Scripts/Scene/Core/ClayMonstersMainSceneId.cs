using Lighthouse.Scene;
using System;

namespace Scene.Core
{
    public static class ClayMonstersMainSceneId
    {
        public static readonly MainSceneId None = new MainSceneId(1, string.Empty);
        public static readonly MainSceneId Title = new MainSceneId(2, "Title");
        public static readonly MainSceneId ModeSelect = new MainSceneId(3, "ModeSelect");
        public static readonly MainSceneId ClayEdit = new MainSceneId(4, "ClayEdit");
        public static readonly MainSceneId BattleNpc = new MainSceneId(5, "BattleNpc");
        public static readonly MainSceneId BattlePVP = new MainSceneId(6, "BattlePVP");

        public static ReadOnlySpan<MainSceneId> All
        {
            get
            {
                return new MainSceneId[]
                {
                    None,
                    Title,
                    ModeSelect,
                    ClayEdit,
                    BattleNpc,
                    BattlePVP,
                };
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Lighthouse.Scene;

namespace Scene.Core
{
    public sealed class SceneGroupProvider : ISceneGroupProvider
    {
        static readonly ModuleSceneId[] RequireSceneModuleIds = Array.Empty<ModuleSceneId>();

        // 各メインシーンが個別に要求するモジュールマップ
        static readonly IReadOnlyDictionary<MainSceneId, ModuleSceneId[]> SceneModuleMap =
            new Dictionary<MainSceneId, ModuleSceneId[]>
            {
                { ClayMonstersMainSceneId.Title, null },
                { ClayMonstersMainSceneId.ModeSelect, null },
                { ClayMonstersMainSceneId.ClayEdit, null },
                { ClayMonstersMainSceneId.BattleNpc, null },
                { ClayMonstersMainSceneId.BattlePVP, null }
            };

        // シーンのグループ構成
        static readonly MainSceneId[][] MainSceneGroupList =
        {
            // グループ1: タイトル単体（または起動、スプラッシュ等）
            new[] { ClayMonstersMainSceneId.Title },
            
            // グループ2: モード選択と、そこから派生するゲーム本編セッション
            new[]
            {
                ClayMonstersMainSceneId.ModeSelect,
                ClayMonstersMainSceneId.ClayEdit,
                ClayMonstersMainSceneId.BattleNpc,
                ClayMonstersMainSceneId.BattlePVP
            }
        };

        static readonly SceneGroup[] SceneGroupList = CreateSceneGroups();

        SceneGroup ISceneGroupProvider.GetSceneGroup(MainSceneId mainSceneId)
        {
            return SceneGroupList.First(sceneGroup => sceneGroup.MainSceneIds.Contains(mainSceneId));
        }

        static SceneGroup[] CreateSceneGroups()
        {
#if UNITY_EDITOR
            var duplicateMainScenes = MainSceneGroupList
                .SelectMany(x => x.Select(y => y))
                .GroupBy(x => x.Name)
                .Where(x => x.Count() != 1)
                .Select(x => x.Key)
                .ToArray();
            if (duplicateMainScenes.Any())
            {
                var duplicateSceneNames = string.Join(", ", duplicateMainScenes);
                throw new ConstraintException($"Duplicate scenes {duplicateSceneNames}");
            }
#endif
            return MainSceneGroupList.Select(CreateSceneGroup).ToArray();
        }

        static SceneGroup CreateSceneGroup(MainSceneId[] mainSceneKeyList)
        {
            return new SceneGroup(mainSceneKeyList.ToDictionary(
                mainSceneKey => mainSceneKey,
                mainSceneKey => RequireSceneModuleIds
                    .Concat(SceneModuleMap[mainSceneKey] ?? Array.Empty<ModuleSceneId>()).ToArray()));
        }
    }
}
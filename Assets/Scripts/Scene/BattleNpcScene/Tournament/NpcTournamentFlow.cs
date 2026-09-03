using Battle;
using Battle.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Localization;
using SaveData;
using SaveData.Interface;
using Scene.TitleScene;
using System.Collections.Generic;
using System.Threading;
using UI.Battle.Interface;
using UnityEngine;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント進行(選択→ブラケット→試合)を担う
    /// </summary>
    public sealed class NpcTournamentFlow
    {
        private readonly IMonsterSelectionSession selectionSession;
        private readonly INpcTournamentBracketView bracketView;
        private readonly IClayModelSaveService saveService;
        private readonly BattleParticipantLoader loader;
        private readonly System.Func<BattleFlow.Context> contextFactory;
        private readonly IBattleView battleView;
        private readonly IBattleStaging staging;
        private readonly IBattleCanvasTransition presentationTransition;
        private readonly Audio.Interface.IBgmService bgmService;
        private readonly Audio.Interface.ISeService seService;
        private readonly System.Action<GameObject, GameObject> registerSpawned;
        private readonly System.Action<bool, int, EnemyStrengthTier> onNpcSettled;
        private readonly IPointsService pointsService;
        private readonly ISkillTreeService skillTreeService;
        private readonly INpcTournamentProgressService tournamentProgress;

        private readonly List<Sprite> runtimeSprites = new List<Sprite>();

        /// <summary>
        /// 依存を受け取る
        /// </summary>
        public NpcTournamentFlow(
            IMonsterSelectionSession selectionSession,
            INpcTournamentBracketView bracketView,
            IClayModelSaveService saveService,
            BattleParticipantLoader loader,
            System.Func<BattleFlow.Context> contextFactory,
            IBattleView battleView,
            IBattleStaging staging,
            IBattleCanvasTransition presentationTransition,
            Audio.Interface.IBgmService bgmService,
            Audio.Interface.ISeService seService,
            System.Action<GameObject, GameObject> registerSpawned,
            System.Action<bool, int, EnemyStrengthTier> onNpcSettled,
            IPointsService pointsService,
            ISkillTreeService skillTreeService,
            INpcTournamentProgressService tournamentProgress)
        {
            this.selectionSession = selectionSession;
            this.bracketView = bracketView;
            this.saveService = saveService;
            this.loader = loader;
            this.contextFactory = contextFactory;
            this.battleView = battleView;
            this.staging = staging;
            this.presentationTransition = presentationTransition;
            this.bgmService = bgmService;
            this.seService = seService;
            this.registerSpawned = registerSpawned;
            this.onNpcSettled = onNpcSettled;
            this.pointsService = pointsService;
            this.skillTreeService = skillTreeService;
            this.tournamentProgress = tournamentProgress;
        }

        /// <summary>
        /// トーナメントを最後まで進める
        /// </summary>
        /// <param name="difficulty">難易度</param>
        /// <param name="resume">中断からの再開か</param>
        /// <param name="cancellationToken">キャンセル</param>
        public async UniTask RunAsync(
            NpcTournamentDifficulty difficulty,
            bool resume,
            CancellationToken cancellationToken)
        {
            if (bracketView == null || !bracketView.IsConfigured)
            {
                Debug.LogError("[NpcTournamentFlow] ブラケットUIが未配線です");
                return;
            }

            GameObject playerModel = null;
            int playerSlotIndex = -1;
            NpcTournamentBracket bracket = null;
            bool shouldClearProgress = true;

            try
            {
                if (resume)
                {
                    (playerModel, playerSlotIndex, bracket, difficulty) =
                        await TryResumeAsync(difficulty, cancellationToken);
                    if (playerModel == null || bracket == null)
                    {
                        tournamentProgress?.ClearProgress();
                        resume = false;
                    }
                }

                if (!resume)
                {
                    tournamentProgress?.ClearProgress();
                    bgmService?.Play(Audio.BgmTrackId.BattleSelection);
                    playerModel = await selectionSession.WaitForModelAsync(cancellationToken);
                    if (playerModel == null)
                    {
                        Debug.LogError("[NpcTournamentFlow] プレイヤー選択に失敗しました");
                        shouldClearProgress = false;
                        return;
                    }

                    playerSlotIndex = selectionSession.SelectedSlotIndex;
                    ModelSaveSlot playerSlot = saveService.GetSlot(
                        selectionSession.SavePool,
                        playerSlotIndex);
                    string playerName = playerSlot != null && !string.IsNullOrEmpty(playerSlot.modelName)
                        ? playerSlot.modelName
                        : "Player";
                    registerSpawned?.Invoke(playerModel, null);

                    List<NpcTournamentFighter> npcs = BuildNpcFighters();
                    if (npcs.Count <= 0)
                    {
                        Debug.LogError("[NpcTournamentFlow] 敵モデルが1体もありません");
                        Object.Destroy(playerModel);
                        playerModel = null;
                        registerSpawned?.Invoke(null, null);
                        return;
                    }

                    EnsureNpcCount(npcs, NpcTournamentBracket.FighterCount - 1);
                    ShuffleInPlace(npcs);

                    bracket = new NpcTournamentBracket();
                    bracket.Initialize(playerName, npcs, difficulty);
                    BindThumbnails(bracket, playerSlotIndex);
                    selectionSession.Hide();

                    bgmService?.Play(Audio.BgmTrackId.Battle);
                    await ShowBracketWithScreenFadeAsync(
                        bracket,
                        cancellationToken,
                        focusPlayerEntry: true);
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                    Canvas.ForceUpdateCanvases();
                    Debug.Log("[NpcTournamentFlow] ブラケット表示後クリック待ち");
                }

                for (int round = bracket.CurrentRound; round <= 2; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!bracket.IsPlayerAlive())
                    {
                        break;
                    }

                    NpcTournamentBracketWaitResult waitResult =
                        await bracketView.WaitForAdvanceOrAbortAsync(cancellationToken);
                    Debug.Log($"[NpcTournamentFlow] 待機結果={waitResult} round={round}");
                    if (waitResult == NpcTournamentBracketWaitResult.AbortToTitle)
                    {
                        SaveProgress(bracket, difficulty, playerSlotIndex);
                        shouldClearProgress = false;
                        break;
                    }

                    ResolveNonPlayerMatches(bracket, round);

                    if (!bracket.IsPlayerAlive())
                    {
                        break;
                    }

                    int playerMatch = bracket.FindPlayerMatchIndex(round);
                    if (playerMatch < 0)
                    {
                        Debug.LogError($"[NpcTournamentFlow] プレイヤー試合が見つかりません round={round}");
                        break;
                    }

                    if (!bracket.TryGetMatchPair(round, playerMatch, out int leftLeaf, out int rightLeaf))
                    {
                        break;
                    }

                    await bracketView.PlayTravelRiseAndShakeAsync(
                        round,
                        playerMatch,
                        leftLeaf,
                        rightLeaf,
                        restoreSlotsAfter: false,
                        cancellationToken);

                    await HideBracketWithScreenFadeAsync(cancellationToken);

                    int opponentLeaf = leftLeaf == bracket.PlayerLeafIndex ? rightLeaf : leftLeaf;
                    NpcTournamentFighter opponent = bracket.Leaves[opponentLeaf];
                    (bool playerWon, bool aborted) = await RunPlayerMatchAsync(
                        playerModel,
                        playerSlotIndex,
                        opponent,
                        cancellationToken);

                    RestorePlayerModel(playerModel);

                    int winnerLeaf = playerWon ? bracket.PlayerLeafIndex : opponentLeaf;
                    int loserLeaf = playerWon ? opponentLeaf : bracket.PlayerLeafIndex;
                    bool isChampionWin = round == 2 && playerWon;

                    if (isChampionWin)
                    {
                        await ShowBracketForChampionRiseAsync(
                            bracket,
                            round,
                            playerMatch,
                            winnerLeaf,
                            loserLeaf,
                            cancellationToken);
                    }
                    else
                    {
                        await ShowBracketResolvedAfterBattleAsync(
                            bracket,
                            round,
                            playerMatch,
                            winnerLeaf,
                            loserLeaf,
                            cancellationToken);
                    }

                    bracket.ApplyMatchResult(round, playerMatch, winnerLeaf);
                    if (playerWon)
                    {
                        onNpcSettled?.Invoke(true, opponent.EnemySlotIndex, opponent.StrengthTier);
                    }
                    else
                    {
                        onNpcSettled?.Invoke(false, opponent.EnemySlotIndex, opponent.StrengthTier);
                    }

                    bracketView.RefreshDefeated(bracket);
                    bracketView.RefreshAdvanceSlots(bracket);

                    if (presentationTransition != null)
                    {
                        await presentationTransition.FadeInAsync(cancellationToken);
                    }

                    if (isChampionWin)
                    {
                        await bracketView.PlayChampionRiseAsync(winnerLeaf, cancellationToken);
                        await PresentChampionRewardAsync(difficulty, cancellationToken);
                        break;
                    }

                    if (!playerWon)
                    {
                        await UniTask.Delay(
                            System.TimeSpan.FromSeconds(1.5f),
                            DelayType.UnscaledDeltaTime,
                            cancellationToken: cancellationToken);
                        break;
                    }

                    if (aborted)
                    {
                        bracket.AdvanceRound();
                        SaveProgress(bracket, difficulty, playerSlotIndex);
                        shouldClearProgress = false;
                        await UniTask.Delay(
                            System.TimeSpan.FromSeconds(1.5f),
                            DelayType.UnscaledDeltaTime,
                            cancellationToken: cancellationToken);
                        break;
                    }

                    bracket.AdvanceRound();
                    SaveProgress(bracket, difficulty, playerSlotIndex);
                    bracketView.Show(bracket);
                }
            }
            finally
            {
                await HideBracketWithScreenFadeAsync(cancellationToken);
                if (playerModel != null)
                {
                    Object.Destroy(playerModel);
                }

                registerSpawned?.Invoke(null, null);
                ClearRuntimeSprites();
                if (shouldClearProgress)
                {
                    tournamentProgress?.ClearProgress();
                }
            }
        }

        private async UniTask<(
            GameObject playerModel,
            int playerSlotIndex,
            NpcTournamentBracket bracket,
            NpcTournamentDifficulty difficulty)> TryResumeAsync(
            NpcTournamentDifficulty fallbackDifficulty,
            CancellationToken cancellationToken)
        {
            NpcTournamentProgressSaveData progress = tournamentProgress?.GetProgressOrNull();
            if (progress == null)
            {
                Debug.LogWarning("[NpcTournamentFlow] 再開進捗が無いため新規開始します");
                return (null, -1, null, fallbackDifficulty);
            }

            NpcTournamentDifficulty difficulty = (NpcTournamentDifficulty)progress.difficulty;
            ModelSaveSlot playerSlot = saveService.GetSlot(
                ModelSavePool.TrainedPlayer,
                progress.playerSlotIndex);
            if (playerSlot == null || string.IsNullOrEmpty(playerSlot.glbFileName))
            {
                Debug.LogError(
                    "[NpcTournamentFlow] 再開用プレイヤースロットが無効です"
                    + $" slot={progress.playerSlotIndex}");
                return (null, -1, null, fallbackDifficulty);
            }

            selectionSession.Hide();
            BattleParticipant loaded = await loader.LoadPlayerSlotAsync(
                progress.playerSlotIndex,
                null,
                cancellationToken);
            if (!loaded.IsValid)
            {
                Debug.LogError("[NpcTournamentFlow] 再開用プレイヤーの読み込みに失敗しました");
                return (null, -1, null, fallbackDifficulty);
            }

            GameObject playerModel = loaded.Model;
            registerSpawned?.Invoke(playerModel, null);

            var bracket = new NpcTournamentBracket();
            bracket.RestoreFromProgress(progress, difficulty);
            BindThumbnails(bracket, progress.playerSlotIndex);

            bgmService?.Play(Audio.BgmTrackId.Battle);
            await ShowBracketWithScreenFadeAsync(
                bracket,
                cancellationToken,
                focusPlayerEntry: true);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
            Debug.Log(
                "[NpcTournamentFlow] トーナメント再開"
                + $" difficulty={difficulty} round={bracket.CurrentRound}"
                + $" playerSlot={progress.playerSlotIndex}");

            return (playerModel, progress.playerSlotIndex, bracket, difficulty);
        }

        private void SaveProgress(
            NpcTournamentBracket bracket,
            NpcTournamentDifficulty difficulty,
            int playerSlotIndex)
        {
            if (tournamentProgress == null || bracket == null)
            {
                return;
            }

            if (playerSlotIndex < 0 || !bracket.IsPlayerAlive() || bracket.IsPlayerChampion())
            {
                tournamentProgress.ClearProgress();
                return;
            }

            var leafEnemy = new int[NpcTournamentBracket.FighterCount];
            var leafNames = new string[NpcTournamentBracket.FighterCount];
            var leafDefeated = new bool[NpcTournamentBracket.FighterCount];
            for (int i = 0; i < NpcTournamentBracket.FighterCount; i++)
            {
                NpcTournamentFighter fighter = bracket.Leaves[i];
                leafEnemy[i] = fighter != null ? fighter.EnemySlotIndex : -1;
                leafNames[i] = fighter != null ? fighter.DisplayName : string.Empty;
                leafDefeated[i] = fighter != null && fighter.IsDefeated;
            }

            var quarter = new int[NpcTournamentBracket.QuarterMatchCount];
            for (int i = 0; i < quarter.Length; i++)
            {
                quarter[i] = bracket.GetQuarterWinner(i);
            }

            var semi = new int[NpcTournamentBracket.SemiMatchCount];
            for (int i = 0; i < semi.Length; i++)
            {
                semi[i] = bracket.GetSemiWinner(i);
            }

            tournamentProgress.SaveProgress(new NpcTournamentProgressSaveData
            {
                hasProgress = true,
                difficulty = (int)difficulty,
                playerSlotIndex = playerSlotIndex,
                currentRound = bracket.CurrentRound,
                leafEnemySlotIndices = leafEnemy,
                leafDisplayNames = leafNames,
                leafDefeated = leafDefeated,
                quarterWinners = quarter,
                semiWinners = semi,
                championLeafIndex = bracket.ChampionLeafIndex,
            });
        }

        private async UniTask PresentChampionRewardAsync(
            NpcTournamentDifficulty difficulty,
            CancellationToken cancellationToken)
        {
            int reward = BattlePointsRules.ResolveTournamentChampionPoints((int)difficulty);
            if (skillTreeService != null)
            {
                reward = skillTreeService.ApplyPointsGainBonus(reward);
            }

            if (pointsService == null)
            {
                Debug.LogError("[NpcTournamentFlow] pointsServiceが未注入です");
            }
            else
            {
                pointsService.AddPoints(reward);
            }

            await bracketView.ShowChampionRewardAsync(reward, cancellationToken);
        }

        private async UniTask ShowBracketWithScreenFadeAsync(
            NpcTournamentBracket bracket,
            CancellationToken cancellationToken,
            bool focusPlayerEntry = false)
        {
            if (presentationTransition != null)
            {
                await presentationTransition.FadeOutAsync(cancellationToken);
            }

            bracketView.Show(bracket);
            if (focusPlayerEntry)
            {
                bracketView.FocusPlayerEntry(bracket);
            }

            if (presentationTransition != null)
            {
                await presentationTransition.FadeInAsync(cancellationToken);
            }

            // 明転後にレイアウトが確定してから再フォーカスする
            if (focusPlayerEntry)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                Canvas.ForceUpdateCanvases();
                bracketView.FocusPlayerEntry(bracket);
            }
        }

        /// <summary>
        /// 戦闘後は暗転のまま表を出して解決し明転で確定後の表だけ見せる
        /// </summary>
        private async UniTask ShowBracketResolvedAfterBattleAsync(
            NpcTournamentBracket bracket,
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            if (presentationTransition != null)
            {
                await presentationTransition.FadeOutAsync(cancellationToken);
            }

            bracketView.Show(bracket);
            await bracketView.PlayPostBattleResolveAsync(
                round,
                matchIndex,
                winnerLeaf,
                loserLeaf,
                cancellationToken);
        }

        /// <summary>
        /// 決勝勝利後は暗転中に交差位置へ置き明転後に上昇する
        /// </summary>
        private async UniTask ShowBracketForChampionRiseAsync(
            NpcTournamentBracket bracket,
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            if (presentationTransition != null)
            {
                await presentationTransition.FadeOutAsync(cancellationToken);
            }

            bracketView.Show(bracket);
            await bracketView.PrepareChampionAtIntersectionAsync(
                round,
                matchIndex,
                winnerLeaf,
                loserLeaf,
                cancellationToken);
        }

        private async UniTask HideBracketWithScreenFadeAsync(CancellationToken cancellationToken)
        {
            if (presentationTransition != null)
            {
                await presentationTransition.FadeOutAsync(cancellationToken);
            }

            bracketView.Hide();
        }

        private static void ResolveNonPlayerMatches(
            NpcTournamentBracket bracket,
            int round)
        {
            int matchCount = round == 0
                ? NpcTournamentBracket.QuarterMatchCount
                : round == 1
                    ? NpcTournamentBracket.SemiMatchCount
                    : 1;

            for (int match = 0; match < matchCount; match++)
            {
                if (!bracket.TryGetMatchPair(round, match, out int left, out int right))
                {
                    continue;
                }

                if (left == bracket.PlayerLeafIndex || right == bracket.PlayerLeafIndex)
                {
                    continue;
                }

                if (round == 0 && bracket.GetQuarterWinner(match) >= 0)
                {
                    continue;
                }

                if (round == 1 && bracket.GetSemiWinner(match) >= 0)
                {
                    continue;
                }

                if (round == 2 && bracket.ChampionLeafIndex >= 0)
                {
                    continue;
                }

                int winner = bracket.ResolveNpcVersusNpcWinner(left, right);
                bracket.ApplyMatchResult(round, match, winner);
            }
        }

        private static void RestorePlayerModel(GameObject playerModel)
        {
            if (playerModel == null)
            {
                return;
            }

            // 次試合でConfigure再Initializeしない前提でも勝利歩き残を消す
            ModelPartLossController partLoss = playerModel.GetComponent<ModelPartLossController>();
            partLoss?.RestoreAll();

            ProceduralMotionCharacter motion = playerModel.GetComponent<ProceduralMotionCharacter>();
            if (motion != null && motion.IsReady)
            {
                motion.ResetForTrainingDisplay();
                motion.ClearBattlePositionConstraint();
                motion.SetRootTranslationEnabled(false);
            }

            if (!playerModel.activeSelf)
            {
                playerModel.SetActive(true);
            }
        }

        private async UniTask<(bool playerWon, bool aborted)> RunPlayerMatchAsync(
            GameObject playerModel,
            int playerSlotIndex,
            NpcTournamentFighter opponent,
            CancellationToken cancellationToken)
        {
            BattleFlow.Context context = contextFactory();
            context.SelectEnemyAfterPlayer = false;
            context.AutoStartMatchup = false;
            context.EnableEnemyStrengthSelect = false;
            context.PreloadedPlayerModel = playerModel;
            // 再開時は選択UIを通らないためSelectedSlotIndexが-1のまま
            context.PreloadedPlayerBuilder = model => loader.BuildFromLoadedModel(
                model,
                ModelSavePool.TrainedPlayer,
                playerSlotIndex,
                context.PlayerSpawn);
            context.EnemySlotIndex = opponent.EnemySlotIndex;
            context.EnemyStrengthTier = opponent.StrengthTier;
            context.UseTournamentVictoryButtons = true;
            if (context.VictoryDualReturnView == null)
            {
                Debug.LogError("[NpcTournamentFlow] 勝利戻りDual UIが未配線です");
            }

            context.RegisterSpawnedParticipants = registerSpawned;
            context.OnNpcBattleSettled = null;
            context.EnemyLoader = ct => loader.LoadEnemyAsync(
                opponent.EnemySlotIndex,
                context.EnemySpawn,
                opponent.StrengthTier,
                ct);

            bool playerWon = false;
            context.OnLocalBattleOutcomeSettled = outcome =>
            {
                playerWon = outcome == BattleLocalOutcome.Win;
            };

            var flow = new BattleFlow(
                selectionSession,
                battleView,
                staging,
                loader,
                context,
                bgmService,
                seService);

            BattleVictoryReturnChoice choice = await flow.RunAsync(cancellationToken);
            bool aborted = playerWon && choice == BattleVictoryReturnChoice.Title;
            return (playerWon, aborted);
        }

        private static void EnsureNpcCount(List<NpcTournamentFighter> npcs, int requiredCount)
        {
            if (npcs == null || npcs.Count <= 0 || npcs.Count >= requiredCount)
            {
                return;
            }

            int sourceCount = npcs.Count;
            int next = 0;
            while (npcs.Count < requiredCount)
            {
                NpcTournamentFighter src = npcs[next % sourceCount];
                npcs.Add(new NpcTournamentFighter
                {
                    EnemySlotIndex = src.EnemySlotIndex,
                    DisplayName = src.DisplayName,
                    StrengthTier = src.StrengthTier,
                });
                next++;
            }
        }

        private List<NpcTournamentFighter> BuildNpcFighters()
        {
            var pool = new List<NpcTournamentFighter>();
            int max = ModelSavePoolSettings.EnemySlotCount;
            for (int i = 0; i < max; i++)
            {
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, i);
                if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
                {
                    continue;
                }

                pool.Add(new NpcTournamentFighter
                {
                    EnemySlotIndex = i,
                    DisplayName = string.IsNullOrEmpty(slot.modelName)
                        ? $"NPC{i + 1}"
                        : EnemyDisplayName.Resolve(i, slot.modelName),
                });
            }

            // 出場枠は毎回ランダムに選ぶ
            ShuffleInPlace(pool);
            int take = Mathf.Min(NpcTournamentBracket.FighterCount - 1, pool.Count);
            if (take <= 0)
            {
                return pool;
            }

            return pool.GetRange(0, take);
        }

        /// <summary>
        /// リストをその場でシャッフルする
        /// </summary>
        /// <param name="list">対象</param>
        private static void ShuffleInPlace(List<NpcTournamentFighter> list)
        {
            if (list == null || list.Count <= 1)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                NpcTournamentFighter tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void BindThumbnails(NpcTournamentBracket bracket, int playerSlotIndex)
        {
            var spriteCache = new Dictionary<string, Sprite>();
            for (int i = 0; i < bracket.Leaves.Count; i++)
            {
                NpcTournamentFighter fighter = bracket.Leaves[i];
                Sprite sprite;
                if (fighter.IsPlayer)
                {
                    sprite = GetOrCreateThumbnailSprite(
                        spriteCache,
                        ModelSavePool.TrainedPlayer,
                        playerSlotIndex);
                }
                else
                {
                    sprite = GetOrCreateThumbnailSprite(
                        spriteCache,
                        ModelSavePool.Enemy,
                        fighter.EnemySlotIndex);
                }

                bracketView.SetLeafThumbnail(i, sprite, fighter.DisplayName);
            }
        }

        private Sprite GetOrCreateThumbnailSprite(
            Dictionary<string, Sprite> cache,
            ModelSavePool pool,
            int slotIndex)
        {
            string key = $"{(int)pool}:{slotIndex}";
            if (cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite created = CreateThumbnailSprite(pool, slotIndex);
            cache[key] = created;
            return created;
        }

        private Sprite CreateThumbnailSprite(ModelSavePool pool, int slotIndex)
        {
            Texture2D texture = saveService.LoadThumbnail(pool, slotIndex);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private void ClearRuntimeSprites()
        {
            for (int i = 0; i < runtimeSprites.Count; i++)
            {
                Sprite sprite = runtimeSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                if (sprite.texture != null)
                {
                    Object.Destroy(sprite.texture);
                }

                Object.Destroy(sprite);
            }

            runtimeSprites.Clear();
        }
    }
}

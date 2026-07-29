using SaveData;
using SaveData.Service;
using UnityEditor;
using UnityEngine;

namespace ClayMonsters.Editor
{
    /// <summary>
    /// 敵セーブスロットをサムネイル一覧からクリックで入れ替えるEditorWindow
    /// </summary>
    public sealed class EnemySlotSwapWindow : EditorWindow
    {
        private const string MenuPath = "ClayMonsters/セーブ/敵スロットを入れ替え";
        private const int ColumnCount = 5;
        private const float CellWidth = 118f;
        private const float ThumbnailSize = 88f;

        private ClayModelSaveService saveService;
        private Texture2D[] thumbnails;
        private string[] slotNames;
        private bool[] slotUsed;
        private Vector2 scroll;
        private int selectedSlotIndex = -1;
        private bool swapNpcBattleStrengthProgress = true;
        private bool mirrorToStreamingAssets = true;
        private string statusMessage = "入れ替えたいスロットを1つ目にクリックしてください";

        /// <summary>
        /// 敵スロット入れ替えWindowを開く
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Open()
        {
            EnemySlotSwapWindow window = GetWindow<EnemySlotSwapWindow>();
            window.titleContent = new GUIContent("敵スロット入れ替え");
            window.minSize = new Vector2(640f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSlots();
        }

        private void OnDisable()
        {
            ClearThumbnails();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawHelpBox();
            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSlotGrid();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(statusMessage, EditorStyles.wordWrappedLabel);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("再読込", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    RefreshSlots();
                    selectedSlotIndex = -1;
                    statusMessage = "一覧を再読込しました入れ替えたいスロットをクリックしてください";
                }

                if (GUILayout.Button("選択解除", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    selectedSlotIndex = -1;
                    statusMessage = "選択を解除しました入れ替えたいスロットをクリックしてください";
                    Repaint();
                }

                GUILayout.FlexibleSpace();
                swapNpcBattleStrengthProgress = GUILayout.Toggle(
                    swapNpcBattleStrengthProgress,
                    "強さ進捗も入替",
                    EditorStyles.toolbarButton);
                mirrorToStreamingAssets = GUILayout.Toggle(
                    mirrorToStreamingAssets,
                    "Streamingへ反映",
                    EditorStyles.toolbarButton);
            }
        }

        private void DrawHelpBox()
        {
            EditorGUILayout.HelpBox(
                "操作: 1つ目のスロットをクリック→入れ替え先をクリックで即入れ替え\n"
                + "同じスロットを再度クリックすると選択解除します",
                MessageType.Info);
        }

        private void DrawSlotGrid()
        {
            EnsureCache();
            int slotCount = ModelSavePoolSettings.EnemySlotCount;
            for (int rowStart = 0; rowStart < slotCount; rowStart += ColumnCount)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int rowEnd = Mathf.Min(rowStart + ColumnCount, slotCount);
                    for (int i = rowStart; i < rowEnd; i++)
                    {
                        DrawSlotCell(i);
                    }

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(6f);
            }
        }

        private void DrawSlotCell(int slotIndex)
        {
            bool selected = selectedSlotIndex == slotIndex;
            bool used = slotUsed != null
                && slotIndex < slotUsed.Length
                && slotUsed[slotIndex];
            string name = slotNames != null && slotIndex < slotNames.Length
                ? slotNames[slotIndex]
                : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                name = used ? "(名前なし)" : "空き";
            }

            Rect cellRect = GUILayoutUtility.GetRect(
                CellWidth,
                CellWidth + 36f,
                GUILayout.Width(CellWidth),
                GUILayout.Height(CellWidth + 36f));

            Color fill = selected
                ? new Color(0.55f, 0.45f, 0.12f, 1f)
                : used
                    ? new Color(0.28f, 0.28f, 0.30f, 1f)
                    : new Color(0.22f, 0.22f, 0.24f, 1f);
            EditorGUI.DrawRect(cellRect, fill);
            if (selected)
            {
                DrawSelectionBorder(cellRect, new Color(1f, 0.85f, 0.25f, 1f), 3f);
            }

            Rect thumbRect = new Rect(
                cellRect.x + 8f,
                cellRect.y + 8f,
                ThumbnailSize,
                ThumbnailSize);
            Texture2D thumb = thumbnails != null && slotIndex < thumbnails.Length
                ? thumbnails[slotIndex]
                : null;
            if (thumb != null)
            {
                GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.22f, 1f));
                GUI.Label(
                    thumbRect,
                    used ? "No Image" : "Empty",
                    EditorStyles.centeredGreyMiniLabel);
            }

            Rect indexRect = new Rect(
                cellRect.x + 8f,
                thumbRect.yMax + 2f,
                cellRect.width - 16f,
                16f);
            GUI.Label(indexRect, $"#{slotIndex + 1}", EditorStyles.boldLabel);
            Rect nameRect = new Rect(
                cellRect.x + 8f,
                indexRect.yMax,
                cellRect.width - 16f,
                16f);
            GUI.Label(nameRect, name, EditorStyles.miniLabel);

            if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
            {
                OnSlotClicked(slotIndex);
            }
        }

        private static void DrawSelectionBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (selectedSlotIndex < 0)
            {
                selectedSlotIndex = slotIndex;
                statusMessage =
                    $"スロット{slotIndex + 1}を選択中入れ替え先をクリックしてください";
                Repaint();
                return;
            }

            if (selectedSlotIndex == slotIndex)
            {
                selectedSlotIndex = -1;
                statusMessage = "選択を解除しました入れ替えたいスロットをクリックしてください";
                Repaint();
                return;
            }

            int from = selectedSlotIndex;
            int to = slotIndex;
            if (!TrySwap(from, to))
            {
                return;
            }

            selectedSlotIndex = -1;
            RefreshSlots();
            statusMessage = $"スロット{from + 1}とスロット{to + 1}を入れ替えました";
            Repaint();
        }

        private bool TrySwap(int slotIndexA, int slotIndexB)
        {
            EnsureSaveService();
            if (!saveService.SwapSlots(ModelSavePool.Enemy, slotIndexA, slotIndexB))
            {
                EditorUtility.DisplayDialog(
                    "敵スロット入れ替え",
                    "入れ替えに失敗しましたConsoleを確認してください",
                    "OK");
                return false;
            }

            if (swapNpcBattleStrengthProgress)
            {
                SwapNpcBattleStrengthProgress(slotIndexA, slotIndexB);
            }

            if (mirrorToStreamingAssets)
            {
                MirrorEnemySlotFilesToStreaming(slotIndexA);
                MirrorEnemySlotFilesToStreaming(slotIndexB);
                MirrorEnemyMetadataToStreaming();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                "[EnemySlotSwapWindow] 敵スロットを入れ替えました"
                + $" a={slotIndexA} b={slotIndexB}"
                + $" progress={swapNpcBattleStrengthProgress}"
                + $" streaming={mirrorToStreamingAssets}");
            return true;
        }

        private void RefreshSlots()
        {
            EnsureSaveService();
            ClearThumbnails();

            int slotCount = ModelSavePoolSettings.EnemySlotCount;
            thumbnails = new Texture2D[slotCount];
            slotNames = new string[slotCount];
            slotUsed = new bool[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, i);
                bool used = slot != null
                    && slot.isUsed
                    && !string.IsNullOrEmpty(slot.glbFileName);
                slotUsed[i] = used;
                slotNames[i] = used && slot != null ? slot.modelName : string.Empty;
                if (!used)
                {
                    continue;
                }

                Texture2D texture = saveService.LoadThumbnail(ModelSavePool.Enemy, i);
                thumbnails[i] = texture;
            }
        }

        private void EnsureSaveService()
        {
            saveService ??= new ClayModelSaveService(new ClayModelGltfExporter());
        }

        private void EnsureCache()
        {
            if (slotUsed == null
                || slotUsed.Length != ModelSavePoolSettings.EnemySlotCount)
            {
                RefreshSlots();
            }
        }

        private void ClearThumbnails()
        {
            if (thumbnails == null)
            {
                return;
            }

            for (int i = 0; i < thumbnails.Length; i++)
            {
                if (thumbnails[i] != null)
                {
                    DestroyImmediate(thumbnails[i]);
                    thumbnails[i] = null;
                }
            }

            thumbnails = null;
        }

        private static void SwapNpcBattleStrengthProgress(int slotIndexA, int slotIndexB)
        {
            SaveDataManager.Update(data =>
            {
                data.NpcBattleProgress ??= new NpcBattleProgressSaveData();
                int maxEnemy = NpcBattleProgressRules.MaxEnemyCount;
                if (data.NpcBattleProgress.slotUnlockedStrengthCounts == null
                    || data.NpcBattleProgress.slotUnlockedStrengthCounts.Length != maxEnemy)
                {
                    data.NpcBattleProgress.slotUnlockedStrengthCounts =
                        NpcBattleProgressRules.CreateInitialSlotStrengthCounts();
                }

                int[] counts = data.NpcBattleProgress.slotUnlockedStrengthCounts;
                if (slotIndexA < 0
                    || slotIndexB < 0
                    || slotIndexA >= counts.Length
                    || slotIndexB >= counts.Length)
                {
                    return;
                }

                (counts[slotIndexA], counts[slotIndexB]) = (counts[slotIndexB], counts[slotIndexA]);
            });
        }

        private static void MirrorEnemySlotFilesToStreaming(int slotIndex)
        {
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetGlbFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetThumbnailFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetTrainingProgressFileName(ModelSavePool.Enemy, slotIndex));
        }

        private static void MirrorEnemyMetadataToStreaming()
        {
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy));
        }
    }
}

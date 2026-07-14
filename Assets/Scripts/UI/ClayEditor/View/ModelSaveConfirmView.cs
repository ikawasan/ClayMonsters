using ClayEditor.Rigging;

using Extensions;

using GameData;

using SaveData;

using System.Collections.Generic;

using TMPro;

using UnityEngine;



namespace UI.ClayEditor.View

{

    /// <summary>

    /// 保存確認画面で名前付きサムネイルと縦並びステータス・技スロットを表示する

    /// レイアウトはプレハブ配置を使い実行時はデータ反映のみ行う

    /// </summary>

    public sealed class ModelSaveConfirmView : MonoBehaviour

    {

        [SerializeField] private GameObject legacyMessageRoot;

        [SerializeField] private RectTransform contentRoot;

        [SerializeField] private ModelSaveSlotRowElementRefs rowElementRefs;



        private ModelSaveSlotRowUiBuilder.RowElements rowElements;

        private Texture2D runtimeThumbnailTexture;

        private Sprite runtimeThumbnailSprite;



        private void Awake()

        {

            BindSceneUi();

        }



        /// <summary>

        /// 保存前プレビューを表示する

        /// </summary>

        public void ShowPreview(

            string modelName,

            byte[] thumbnailPng,

            int slotIndex,

            ModelStatus status,

            IReadOnlyList<MotionType> registeredAttackMotions)

        {

            BindSceneUi();

            SetLegacyMessageVisible(false);

            ModelSaveSlotRowUiBuilder.BindConfirmPreview(

                rowElements,

                modelName,

                slotIndex,

                status,

                registeredAttackMotions);

            ApplyThumbnail(thumbnailPng);

        }



        /// <summary>

        /// 保存済みスロット内容を表示する

        /// </summary>

        public void ShowSlot(ModelSaveSlot slot, byte[] thumbnailPng, int slotIndex)

        {

            BindSceneUi();

            SetLegacyMessageVisible(false);

            ModelSaveSlotRowUiBuilder.BindConfirmFromSlot(rowElements, slot, slotIndex);

            ApplyThumbnail(thumbnailPng);

        }



        /// <summary>

        /// 表示をクリアする

        /// </summary>

        public void Clear()

        {

            ClearThumbnail();



            if (rowElements != null)

            {

                ModelSaveSlotRowUiBuilder.BindConfirmEmpty(rowElements, string.Empty, 0);

            }



            SetLegacyMessageVisible(true);

        }



        private void OnDestroy()

        {

            ClearThumbnail();

        }



        private void BindSceneUi()

        {

            TryResolveLegacyMessageRoot();



            if (contentRoot == null)

            {

                Transform existing = transform.Find("ConfirmContent");

                if (existing is RectTransform rectTransform)

                {

                    contentRoot = rectTransform;

                }

            }



            if (rowElementRefs == null && contentRoot != null)

            {

                Transform rowTransform = contentRoot.Find("ConfirmSlotRow");

                if (rowTransform != null)

                {

                    rowElementRefs = rowTransform.GetComponent<ModelSaveSlotRowElementRefs>();

                }

            }



            if (rowElementRefs != null)

            {

                rowElements = rowElementRefs.ToRowElements();

            }

        }



        private void ApplyThumbnail(byte[] thumbnailPng)

        {

            ClearThumbnail();



            if (rowElements?.ThumbnailImage == null)

            {

                return;

            }



            if (thumbnailPng == null || thumbnailPng.Length == 0)

            {

                ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(rowElements, null);

                return;

            }



            runtimeThumbnailTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!runtimeThumbnailTexture.LoadImage(thumbnailPng))

            {

                ClearThumbnail();

                ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(rowElements, null);

                return;

            }



            runtimeThumbnailSprite = Sprite.Create(

                runtimeThumbnailTexture,

                new Rect(0f, 0f, runtimeThumbnailTexture.width, runtimeThumbnailTexture.height),

                new Vector2(0.5f, 0.5f));



            ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(rowElements, runtimeThumbnailSprite);

        }



        private void ClearThumbnail()

        {

            if (rowElements != null)

            {

                ModelSaveSlotRowUiBuilder.ApplyThumbnailSprite(rowElements, null);

            }



            if (runtimeThumbnailSprite != null)

            {

                Destroy(runtimeThumbnailSprite);

                runtimeThumbnailSprite = null;

            }



            if (runtimeThumbnailTexture != null)

            {

                Destroy(runtimeThumbnailTexture);

                runtimeThumbnailTexture = null;

            }

        }



        private void SetLegacyMessageVisible(bool visible)

        {

            if (legacyMessageRoot != null)

            {

                legacyMessageRoot.SetActive(visible);

            }

        }



        private void TryResolveLegacyMessageRoot()

        {

            if (legacyMessageRoot != null)

            {

                return;

            }



            Transform parent = transform.parent;

            if (parent == null)

            {

                return;

            }



            for (int i = 0; i < parent.childCount; i++)

            {

                Transform child = parent.GetChild(i);

                if (child == transform || child.name != "Text (TMP)")

                {

                    continue;

                }



                legacyMessageRoot = child.gameObject;

                return;

            }

        }

    }

}


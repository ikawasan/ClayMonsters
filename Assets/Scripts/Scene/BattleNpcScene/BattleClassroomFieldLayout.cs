using UnityEngine;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleClassroom教室の配置定数と明示参照による表示切替
    /// Field参照の探索は行わない各シーンがSerializeFieldで渡す
    /// </summary>
    public static class BattleClassroomFieldLayout
    {
        /// <summary>
        /// 戦闘用教室ルートのオブジェクト名
        /// </summary>
        public const string FieldObjectName = "Field";

        public const float FieldFloorLocalY = 0.605f;
        public const float HorizontalAngle = 0f;
        public const float VerticalAngle = 10f;
        public const float CameraDistance = 7.5f;
        public const float FocusHeightOffset = 0.75f;

        /// <summary>
        /// 指定したFieldの表示を切り替える
        /// </summary>
        /// <param name="field">対象Field</param>
        /// <param name="isVisible">表示するか</param>
        public static void SetFieldVisible(GameObject field, bool isVisible)
        {
            if (field == null)
            {
                Debug.LogError("[BattleClassroomFieldLayout] Field参照がありません");
                return;
            }

            if (field.activeSelf == isVisible)
            {
                return;
            }

            field.SetActive(isVisible);
        }
    }
}

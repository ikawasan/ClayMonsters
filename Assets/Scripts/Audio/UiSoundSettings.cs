using UnityEngine;

namespace Audio
{
    /// <summary>
    /// UI効果音クリップの設定
    /// </summary>
    [CreateAssetMenu(fileName = "UiSoundSettings", menuName = "ClayMonsters/UiSoundSettings")]
    public sealed class UiSoundSettings : ScriptableObject
    {
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float hoverVolumeScale = 0.35f;
        [SerializeField, Range(0f, 1f)] private float clickVolumeScale = 0.7f;

        /// <summary>
        /// ボタンホバー音
        /// </summary>
        public AudioClip HoverClip => hoverClip;

        /// <summary>
        /// ボタン押下音
        /// </summary>
        public AudioClip ClickClip => clickClip;

        /// <summary>
        /// ホバー音の音量倍率
        /// </summary>
        public float HoverVolumeScale => hoverVolumeScale;

        /// <summary>
        /// 押下音の音量倍率
        /// </summary>
        public float ClickVolumeScale => clickVolumeScale;
    }
}

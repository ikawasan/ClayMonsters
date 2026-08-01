using R3;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// モデル名の入力を扱うView
    /// TMP_InputFieldの入力を受け取り、最大文字数の制限と空文字判定を行い、確定名を通知する
    /// </summary>
    public class ModelNameInputView : MonoBehaviour
    {
        [Header("入力")]
        [Tooltip("名前を入力するInputField")]
        [SerializeField] private TMP_InputField inputField;

        [Header("制約")]
        [Tooltip("名前の最大文字数")]
        [SerializeField] private int maxLength = 8;

        [Tooltip("空文字のときに使う既定名")]
        [SerializeField] private string defaultName = "Monster";

        private readonly ReactiveProperty<string> currentName = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// 現在入力されている名前(変更を購読できる)
        /// </summary>
        public ReadOnlyReactiveProperty<string> CurrentName => currentName;

        /// <summary>
        /// 入力欄のRectTransform
        /// </summary>
        public RectTransform InputFieldRect =>
            inputField != null ? inputField.transform as RectTransform : null;

        /// <summary>
        /// 現在の名前が有効か(空文字でないか)
        /// </summary>
        public bool HasValidName => !string.IsNullOrWhiteSpace(currentName.Value);

        private void Start()
        {
            if (inputField != null)
            {
                // 最大文字数を反映する
                inputField.characterLimit = maxLength;

                // 入力変更を購読して名前を更新する
                inputField.onValueChanged.AsObservable()
                    .Subscribe(OnInputChanged)
                    .AddTo(this);

                // 初期値を反映する
                OnInputChanged(inputField.text);
            }
        }

        /// <summary>
        /// 入力欄へフォーカスを移す
        /// </summary>
        public void FocusInput()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.Select();
            inputField.ActivateInputField();
        }

        /// <summary>
        /// 確定した名前を返す
        /// 入力が空文字のときは既定名を返す
        /// </summary>
        /// <returns>確定した名前</returns>
        public string GetConfirmedName()
        {
            string trimmed = currentName.Value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return defaultName;
            }

            return trimmed;
        }

        /// <summary>
        /// 入力欄へ名前を設定する(既存スロットの編集時などに使う)
        /// </summary>
        /// <param name="name">設定する名前</param>
        public void SetName(string name)
        {
            if (inputField != null)
            {
                inputField.text = name ?? string.Empty;
            }
            else
            {
                currentName.Value = name ?? string.Empty;
            }
        }

        /// <summary>
        /// 入力欄を空にする
        /// </summary>
        public void Clear()
        {
            SetName(string.Empty);
        }

        // 入力欄の値が変わったときに名前を更新する
        private void OnInputChanged(string value)
        {
            // 最大文字数を超える分は切り詰める(characterLimitで基本は防がれるが念のため)
            if (value != null && value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
                if (inputField != null)
                {
                    inputField.SetTextWithoutNotify(value);
                }
            }

            currentName.Value = value ?? string.Empty;
        }

        private void OnDestroy()
        {
            currentName.Dispose();
        }
    }
}
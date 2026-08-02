namespace Extensions
{
    /// <summary>
    /// 入力アイコン付き操作ガイド文言
    /// </summary>
    public static class InputGuideTexts
    {
        /// <summary>
        /// ClayEdit成形ガイド
        /// </summary>
        public static string ClayEditClayGuide =>
            "【成形操作】\n"
            + $"{I("MouseLeft")} ドラッグ : 盛る\n"
            + $"{W("KeyCtrl")}+{I("MouseLeft")} ドラッグ : 削る\n"
            + $"{I("MouseRight")} ドラッグ : 削る\n"
            + $"{I("MouseWheel")} : ブラシサイズ変更\n"
            + $"{W("KeyShift")}+ドラッグ : 深度固定\n"
            + $"{W("KeyAlt")}+ドラッグ : カメラ操作\n"
            + $"{W("KeyCtrl")}+{I("KeyZ")} : 取り消し\n"
            + $"{W("KeyCtrl")}+{I("KeyY")} : やり直し\n"
            + $"{W("KeyDel")} : 全削除";

        /// <summary>
        /// ClayEditペイントガイド
        /// </summary>
        public static string ClayEditPaintGuide =>
            "【ペイント操作】\n"
            + $"{I("MouseLeft")} ドラッグ : 塗る\n"
            + $"{I("MouseWheel")} : ブラシサイズ変更\n"
            + $"{W("KeyShift")}+ドラッグ : 深度固定\n"
            + $"{W("KeyAlt")}+ドラッグ : カメラ操作\n"
            + $"{W("KeyCtrl")}+{I("KeyZ")} : 取り消し\n"
            + $"{W("KeyCtrl")}+{I("KeyY")} : やり直し\n"
            + "色選択 : カラーピッカー";

        /// <summary>
        /// 戦闘チュートリアル本体
        /// </summary>
        public static string BattleTipsBody =>
            "【操作】\n"
            + $"{I("KeyA")} / {I("KeyLeft")} : 後退\n"
            + $"{I("KeyD")} / {I("KeyRight")} : 接近\n"
            + $"{W("KeyShift")}+{I("KeyA")}/{I("KeyD")} : ステップ移動\n"
            + $"{I("Key1")}〜{I("Key4")} : 技を出す\n"
            + $"{I("KeySpace")} : ふきとばし\n"
            + $"{I("MouseRight")} 長押し : 部位復旧\n"
            + "\n"
            + "【戦闘の基本】\n"
            + "・コストを溜めて技を使います\n"
            + "・技ごとに間合いと威力が違います\n"
            + "・同じ技ボタンでカウンターできます\n"
            + "・部位を破壊すると相手が弱体化し、\n"
            + "　破壊された部位を使用する技が使えなくなります。";

        /// <summary>
        /// 戦闘チュートリアル右端ヒント
        /// </summary>
        public static string BattleTipsEscHint =>
            $"{E("KeyEsc")} でチュートリアル表示";

        /// <summary>
        /// 部位復旧の戦闘中ヒント
        /// </summary>
        public static string BattleHintPartRepair =>
            $"{I("MouseRight")}長押しで部位復旧";

        /// <summary>
        /// ふきとばし可能の戦闘中ヒント
        /// </summary>
        public static string BattleHintKnockbackReady =>
            $"ふきとばし可能 {I("KeySpace")}";

        private static string I(string iconName)
        {
            return InputIconTmpUtility.Icon(iconName);
        }

        private static string W(string iconName)
        {
            // 修飾キーは文字が細かいので少しだけ大きくする
            return $"<size=120%>{I(iconName)}</size>";
        }

        private static string E(string iconName)
        {
            // 右端ヒントは本文より小さいためEscを読みやすくする
            return $"<size=145%>{I(iconName)}</size>";
        }
    }
}

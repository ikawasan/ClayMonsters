using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Paint
{
    /// <summary>
    /// ペイントの1回の塗り操作（中心 半径 色）
    /// </summary>
    public struct PaintDab
    {
        /// <summary>塗りの中心（ワールド座標）</summary>
        public Vector3 worldCenter;

        /// <summary>塗りの半径（ワールド単位）</summary>
        public float worldRadius;

        /// <summary>塗る色</summary>
        public Color color;

        /// <summary>塗り時の面法線（ワールド座標）</summary>
        public Vector3 worldNormal;
    }

    /// <summary>
    /// ペイントのUndo/Redoをストローク記録で管理する
    /// 押下から離すまでを1ストローク（塗り操作の列）とし ストローク単位で取り消す
    /// 全ストロークを保持し Undoは末尾ストロークを外して残りを塗り直す
    /// </summary>
    public class ClayPaintHistoryManager
    {
        // 確定済みのストローク列（古い順 各ストロークは塗り操作の列）
        private readonly List<List<PaintDab>> strokes = new();

        // Undoで取り除いたストローク（Redo用）
        private readonly List<List<PaintDab>> redoStrokes = new();

        // 現在記録中のストローク（押下中）
        private List<PaintDab> currentStroke;

        /// <summary>
        /// 確定済みストローク列（塗り直し用に古い順で参照する）
        /// </summary>
        public IReadOnlyList<List<PaintDab>> Strokes => strokes;

        /// <summary>
        /// 新しいストロークを開始する（押下時に呼ぶ）
        /// </summary>
        public void BeginStroke()
        {
            currentStroke = new List<PaintDab>();
        }

        /// <summary>
        /// 現在のストロークへ塗り操作を追加する（押下中の毎フレームに呼ぶ）
        /// </summary>
        /// <param name="dab">追加する塗り操作</param>
        public void AddDab(PaintDab dab)
        {
            if (currentStroke == null)
            {
                currentStroke = new List<PaintDab>();
            }

            currentStroke.Add(dab);
        }

        /// <summary>
        /// 現在のストロークを確定する（離した時に呼ぶ）Redo履歴は破棄する
        /// 塗り操作が1つも無い場合は何もしない
        /// </summary>
        public void EndStroke()
        {
            if (currentStroke == null || currentStroke.Count == 0)
            {
                currentStroke = null;
                return;
            }

            strokes.Add(currentStroke);
            currentStroke = null;
            redoStrokes.Clear();
        }

        /// <summary>
        /// 直前のストロークを取り消す 取り消せた場合true
        /// </summary>
        public bool TryUndo()
        {
            if (strokes.Count == 0)
            {
                return false;
            }

            int last = strokes.Count - 1;
            redoStrokes.Add(strokes[last]);
            strokes.RemoveAt(last);
            return true;
        }

        /// <summary>
        /// 取り消したストロークをやり直す やり直せた場合true
        /// </summary>
        public bool TryRedo()
        {
            if (redoStrokes.Count == 0)
            {
                return false;
            }

            int last = redoStrokes.Count - 1;
            strokes.Add(redoStrokes[last]);
            redoStrokes.RemoveAt(last);
            return true;
        }

        /// <summary>
        /// 履歴をすべて破棄する
        /// </summary>
        public void Clear()
        {
            strokes.Clear();
            redoStrokes.Clear();
            currentStroke = null;
        }
    }
}
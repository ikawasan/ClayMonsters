using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor
{
    public class ClayHistoryManager
    {
        private readonly List<float[]> undoList = new List<float[]>();
        private readonly List<float[]> redoList = new List<float[]>();
        private const int MaxHistory = 20;

        public void SaveState(float[] currentState)
        {
            undoList.Add(currentState);
            if (undoList.Count > MaxHistory)
            {
                undoList.RemoveAt(0);
            }
            redoList.Clear();
        }

        public bool TryUndo(float[] currentState, out float[] previousState)
        {
            if (undoList.Count > 0)
            {
                redoList.Add(currentState);
                previousState = undoList[undoList.Count - 1];
                undoList.RemoveAt(undoList.Count - 1);
                return true;
            }
            previousState = null;
            return false;
        }

        public bool TryRedo(float[] currentState, out float[] nextState)
        {
            if (redoList.Count > 0)
            {
                undoList.Add(currentState);
                nextState = redoList[redoList.Count - 1];
                redoList.RemoveAt(redoList.Count - 1);
                return true;
            }
            nextState = null;
            return false;
        }
    }
}

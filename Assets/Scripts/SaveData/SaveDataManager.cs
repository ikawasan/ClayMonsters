using System.IO;
using UnityEngine;

namespace SaveData
{
    public static class SaveDataManager
    {
        private static string SavePath = Path.Combine(Application.persistentDataPath, "savedata.json");

        public static SaveData Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<SaveData>(json);
            }

            // ファイルが存在しない場合は初期値のデータを返す
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
    }
}

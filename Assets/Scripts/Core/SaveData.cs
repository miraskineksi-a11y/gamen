using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>Прогресс игрока. Сохраняется в PlayerPrefs в виде JSON.</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "caribbean_treasure_save_v1";

        public int version = 1;
        public int gold = 120;
        public int hullLevel, sailLevel, cannonLevel;
        public bool[] fragments = new bool[3];
        public List<string> dug = new List<string>();
        public bool krakenDefeated;
        public bool legendFound;
        public float shipHealth = -1f;
        public int shipsSunk, monstersSlain, enemiesDefeated, treasuresFound;
        public float playTime;
        public bool tutorialSailing, tutorialFoot;

        public int FragmentCount
        {
            get
            {
                int n = 0;
                if (fragments != null) foreach (var f in fragments) if (f) n++;
                return n;
            }
        }

        public bool HasAllFragments => FragmentCount >= 3;

        public static bool Exists => PlayerPrefs.HasKey(Key);

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static SaveData Load()
        {
            if (!Exists) return new SaveData();
            try
            {
                var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                if (d.fragments == null || d.fragments.Length != 3) d.fragments = new bool[3];
                if (d.dug == null) d.dug = new List<string>();
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Сохранение повреждено, начинаем заново: " + e.Message);
                return new SaveData();
            }
        }

        public static void Delete() => PlayerPrefs.DeleteKey(Key);

        // ----- характеристики корабля игрока по уровням улучшений -----
        public float ShipMaxHealth => 100f + 40f * hullLevel;
        public float ShipMaxSpeed => 11f + 1.5f * sailLevel;
        public float ShipTurnRate => 30f + 3f * sailLevel;
        public int GunsPerSide => 3 + Mathf.Min(2, cannonLevel);
        public float CannonDamage => 14f + 4f * cannonLevel;
        public float ReloadTime => 3.2f - 0.3f * cannonLevel;

        public static readonly int[] HullCost = { 200, 400, 700 };
        public static readonly int[] SailCost = { 150, 300, 550 };
        public static readonly int[] CannonCost = { 250, 450, 800 };
    }
}

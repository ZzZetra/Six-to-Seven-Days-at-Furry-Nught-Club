using UnityEngine;

// Деньги игрока (за верные ответы на звонки). Хранятся между запусками в PlayerPrefs;
// счётчик — в главном меню.
public static class Wallet
{
    const string Key = "SSDFNC.Money";

    public static int Money => PlayerPrefs.GetInt(Key, 0);

    public static void Add(int amount)
    {
        PlayerPrefs.SetInt(Key, Money + amount);
        PlayerPrefs.Save();
    }
}

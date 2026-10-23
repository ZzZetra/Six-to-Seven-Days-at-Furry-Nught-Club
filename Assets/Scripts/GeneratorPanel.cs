using UnityEngine;
using UnityEngine.UI;

// Табло генератора: лампа и состояние, полоски здоровья и бензина, остаток в канистрах,
// кнопка «Залить» (держать — бензин переливается из канистр в бак, только вблизи генератора).
public class GeneratorPanel : MonoBehaviour
{
    static readonly Color RunningColor = new Color(0.2f, 0.85f, 0.3f);
    static readonly Color StalledColor = new Color(1f, 0.8f, 0.1f);
    static readonly Color BrokenColor = new Color(0.9f, 0.15f, 0.15f);

    public Generator generator;
    public Image lamp;
    public Text stateLabel;
    public RectTransform healthFill;
    public RectTransform fuelFill;
    public Text reserveLabel;
    public HoldButton refuelButton;

    void Update()
    {
        if (refuelButton.IsHeld && generator.InReach(Camera.main.transform.position))
            generator.Pour(Time.deltaTime);

        var (text, color) = generator.Current switch
        {
            Generator.State.Running => ("РАБОТАЕТ", RunningColor),
            Generator.State.Stalled => ("ЗАГЛОХ — дёрни ручку", StalledColor),
            _ => ("СЛОМАН", BrokenColor),
        };
        lamp.color = color;
        stateLabel.text = text;
        stateLabel.color = color;

        // Ширина заливки полоски = доля от полного
        healthFill.anchorMax = new Vector2(generator.Health / Generator.MaxHealth, 1f);
        fuelFill.anchorMax = new Vector2(generator.Fuel / Generator.TankSize, 1f);
        reserveLabel.text = $"В канистрах: {Mathf.CeilToInt(generator.Reserve)}";
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

// Атака Новы (мог-батл) по фазам и по раскладке под музыку (MogChart из StreamingAssets/MogCharts):
//   1 Появление — Нова повернула игрока к дэнс полу, анимация (заглушка) до первого скилл-чека;
//   2 Кружок, 3 Полоска, 4 В стороны — группы скилл-чеков из раскладки (пока все работают как «полоска»);
//   5 Уход Новы или Смерть — анимация (заглушка).
// Фаза пишется в правом верхнем углу. Время чеков считается по музыке (если она есть), поэтому их
// подгоняют под бит руками в json. ПРОБЕЛ в зелёной зоне — попал. Запас мога: с 3, попал — +1 (до 4),
// промах — −1; на нуле Нова нападает сразу, дожил до конца хотя бы с 1 — она уходит.
public class MogBattle : MonoBehaviour
{
    public enum Phase { None, Intro, Checks, Outro }

    public GameObject panel;       // окно скилл-чека
    public RectTransform zone;     // зелёная зона внутри полоски (anchorMin.x / anchorMax.x)
    public RectTransform marker;   // метка (anchorMin.x = anchorMax.x = позиция)
    public Text title;             // «Полоска 2/5» или «Приготовься: Полоска»
    public Text score;             // запас мога: «Мог: 3 / 4»
    public Text phaseLabel;        // правый верхний угол: «Фаза 3/5: Полоска · 2/5»
    public AudioSource music;

    public Phase Current { get; private set; }
    public bool Running => Current != Phase.None;
    public MogTimeline Timeline { get; private set; }
    public float Clock => clock;

    List<MogChart> charts;
    MogChart lastChart;
    Action<bool> done;
    Transform novaBody;
    Vector3 bodyPos, bodyScale;
    Quaternion bodyRot;
    float clock, outroTime;
    bool clockRunning, won;

    public void Begin(int novaWins, Transform novaBody, Action<bool> onDone)
    {
        if (charts == null) charts = MogCharts.LoadAll();
        var chart = Pick();
        float zoneScale = Mathf.Max(Balance.MogMinZoneScale, 1f - Balance.MogZoneShrinkPerWin * novaWins);
        Timeline = new MogTimeline(chart, zoneScale);
        Timeline.CheckResolved += (c, hit) =>
            Debug.Log($"[SSDFNC] Мог-батл: {MogTimeline.PhaseName(c.Type)} на {c.hit:0.00} с — " +
                      $"{(hit ? "попал" : "мимо")}, мог {Timeline.Lives}");

        done = onDone;
        this.novaBody = novaBody;
        if (novaBody != null)
        {
            bodyPos = novaBody.localPosition;
            bodyScale = novaBody.localScale;
            bodyRot = novaBody.localRotation;
        }
        clock = 0f;
        clockRunning = false;
        won = false;
        Current = Phase.Intro;
        panel.SetActive(false);
        score.text = "";
        Debug.Log($"[SSDFNC] Мог-батл: раскладка «{chart.name}», чеков {Timeline.Count}, музыка: " +
                  $"{(string.IsNullOrEmpty(chart.music) ? "нет" : chart.music)}, зоны ×{zoneScale:0.0}");
        StartCoroutine(StartMusic(chart));
    }

    // Случайная раскладка, по возможности не та же, что в прошлый раз
    MogChart Pick()
    {
        var pool = charts.Count > 1 ? charts.FindAll(c => c != lastChart) : charts;
        lastChart = pool[UnityEngine.Random.Range(0, pool.Count)];
        return lastChart;
    }

    // Музыка грузится, пока идёт анимация появления; время раскладки стартует вместе с музыкой
    IEnumerator StartMusic(MogChart chart)
    {
        string path = MogCharts.MusicPath(chart);
        if (path != null)
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace('\\', '/'), AudioTypeOf(path)))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    music.clip = DownloadHandlerAudioClip.GetContent(req);
                    music.Play();
                }
                else Debug.LogWarning($"[SSDFNC] Музыка {chart.music} не загрузилась: {req.error}. Играем без неё");
            }
        }
        clockRunning = true;
    }

    static AudioType AudioTypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.UNKNOWN;
        }
    }

    void Update()
    {
        if (Current == Phase.None) return;

        if (clockRunning)
            clock = music.clip != null && music.isPlaying
                ? (float)music.timeSamples / music.clip.frequency - Timeline.Chart.offset
                : clock + Time.deltaTime;

        switch (Current)
        {
            case Phase.Intro:
                AnimateIntro();
                if (clockRunning && clock >= Timeline.Chart.FirstCheckStart) EnterChecks();
                break;
            case Phase.Checks:
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame) Timeline.Press(clock);
                Timeline.Update(clock);
                if (Timeline.Done) EnterOutro();
                break;
            case Phase.Outro:
                outroTime += Time.deltaTime;
                AnimateOutro(Mathf.Clamp01(outroTime / Balance.MogOutroTime));
                if (outroTime >= Balance.MogOutroTime) Finish();
                break;
        }
        DrawUI();
    }

    void EnterChecks()
    {
        RestoreBody();
        Current = Phase.Checks;
        Debug.Log("[SSDFNC] Мог-батл: скилл-чеки");
    }

    void EnterOutro()
    {
        won = Timeline.PlayerWon;
        Current = Phase.Outro;
        outroTime = 0f;
        panel.SetActive(false);
        Debug.Log($"[SSDFNC] Мог-батл: попаданий {Timeline.Hits}, промахов {Timeline.Misses}, мог {Timeline.Lives}" +
                  $" — {(won ? "Нова уходит" : $"смерть на чеке {Timeline.Index} из {Timeline.Count}")}");
    }

    void Finish()
    {
        RestoreBody();
        music.Stop();
        if (music.clip != null) // клип грузится заново каждый батл — старый выгружаем
        {
            Destroy(music.clip);
            music.clip = null;
        }
        panel.SetActive(false);
        phaseLabel.text = "";
        Current = Phase.None;
        done?.Invoke(won);
    }

    void DrawUI()
    {
        switch (Current)
        {
            case Phase.Intro:
                phaseLabel.text = "Фаза 1/5: Появление";
                break;
            case Phase.Checks:
            {
                var type = Timeline.Current.Type;
                string name = MogTimeline.PhaseName(type);
                var (number, total) = Timeline.ProgressInType();
                bool active = Timeline.Active(clock);
                phaseLabel.text = $"Фаза {MogTimeline.PhaseNumber(type)}/5: {name} · {number}/{total}";
                panel.SetActive(true);
                zone.gameObject.SetActive(active);
                marker.gameObject.SetActive(active);
                // «Приготовься» — только в паузе перед новой фазой, между чеками одной фазы — просто номер
                title.text = active || number > 1
                    ? $"{name} {number}/{total}" + (type == MogCheckType.Bar ? "" : "  (пока полоской)")
                    : $"Приготовься: {name}";
                if (active)
                {
                    float half = Timeline.ZoneWidth / 2f, center = Timeline.Current.pos;
                    zone.anchorMin = new Vector2(Mathf.Clamp01(center - half), 0f);
                    zone.anchorMax = new Vector2(Mathf.Clamp01(center + half), 1f);
                    float p = Mathf.Clamp01(Timeline.MarkerPos(clock));
                    marker.anchorMin = marker.anchorMax = new Vector2(p, 0.5f);
                }
                DrawLives();
                break;
            }
            case Phase.Outro:
                phaseLabel.text = won ? "Фаза 5/5: Нова уходит" : "Фаза 5/5: Смерть";
                break;
        }
    }

    // «Мог: ■■■□  3 / 4», на последней единице — красным
    void DrawLives()
    {
        int lives = Timeline.Lives;
        score.text = $"Мог: <size=70>{new string('■', lives)}{new string('□', Balance.MogMaxLives - lives)}</size>  {lives} / {Balance.MogMaxLives}";
        score.color = lives <= 1 ? new Color(1f, 0.3f, 0.25f) : Color.white;
    }

    // Заглушки анимаций на капсуле Новы: появление — кружится и пульсирует,
    // уход — сжимается и уходит в пол, смерть — растёт и поднимается на игрока
    void AnimateIntro()
    {
        if (novaBody == null) return;
        float k = Timeline.Chart.FirstCheckStart > 0f ? Mathf.Clamp01(clock / Timeline.Chart.FirstCheckStart) : 1f;
        novaBody.localRotation = bodyRot * Quaternion.Euler(0f, 720f * k, 0f);
        novaBody.localScale = bodyScale * (1f + 0.15f * Mathf.Sin(Time.time * 8f));
    }

    void AnimateOutro(float k)
    {
        if (novaBody == null) return;
        if (won)
        {
            novaBody.localScale = bodyScale * (1f - k);
            novaBody.localPosition = bodyPos + Vector3.down * k;
        }
        else
        {
            novaBody.localScale = bodyScale * (1f + 1.5f * k);
            novaBody.localPosition = bodyPos + Vector3.up * 0.8f * k;
        }
    }

    void RestoreBody()
    {
        if (novaBody == null) return;
        novaBody.localPosition = bodyPos;
        novaBody.localScale = bodyScale;
        novaBody.localRotation = bodyRot;
    }
}

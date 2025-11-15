using System;
using Fusion;
using UnityEngine;
using TMPro;
using System.Collections;

public enum GameModifier
{
    None,
    LowGravity,
    KnockbackX1000,
    AcidColors,
    TinyArena,
    ReversedControls
}

public class RouletteModifierManager : NetworkBehaviour
{
    public static RouletteModifierManager Instance;
    public static bool ReverseControls;

    [SerializeField] TextMeshProUGUI modifierLabel;
    [SerializeField] float labelShowTime = 2f;

    [SerializeField] float defaultGravity = -50f;
    [SerializeField] float defaultKnockback = 14f;

    [SerializeField] float lowGravityValue = -15f;
    [SerializeField] float crazyKnockbackValue = 300f;

    [SerializeField] Color normalTint = Color.white;
    [SerializeField] Color acidTint = new Color(0.5f, 1f, 0.5f, 1f);
    [SerializeField] SpriteRenderer[] extraTintRenderers;

    [SerializeField] Transform[] arenaObjectsToScale;
    [SerializeField] float tinyScale = 0.5f;
    [SerializeField] float normalScale = 1f;
    private Coroutine tinyArenaRoutine;

    [Networked] public GameModifier CurrentModifier { get; set; }
    [Networked] TickTimer NextRollTimer { get; set; }

    GameModifier _lastModifier = GameModifier.None;
    float _labelTimer;

    

    void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            NextRollTimer = TickTimer.CreateFromSeconds(Runner, 1f);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (CurrentModifier != _lastModifier)
        {
            _lastModifier = CurrentModifier;
            ApplyModifier();
        }

        if (NextRollTimer.ExpiredOrNotRunning(Runner))
        {
            Roll();
            NextRollTimer = TickTimer.CreateFromSeconds(Runner, 5f);
        }
    }

    void Roll()
    {
        Array values = Enum.GetValues(typeof(GameModifier));
        int i = UnityEngine.Random.Range(1, values.Length);
        CurrentModifier = (GameModifier)values.GetValue(i);
        Debug.Log($"[Roulette] Rolled: {CurrentModifier}");
    }

    void ApplyModifier()
    {
        Debug.Log($"[Roulette] Apply: {CurrentModifier}");

        ReverseControls = false;

        var players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.gravity   = defaultGravity;
            p.knockback = defaultKnockback;

            var srP = p.GetComponent<SpriteRenderer>();
            if (srP != null) srP.color = normalTint;
        }

        if (arenaObjectsToScale != null)
        {
            foreach (var t in arenaObjectsToScale)
                if (t != null)
                    t.localScale = Vector3.one * normalScale;
        }

        if (extraTintRenderers != null)
        {
            foreach (var r in extraTintRenderers)
                if (r != null)
                    r.color = normalTint;
        }

        switch (CurrentModifier)
        {
            case GameModifier.LowGravity:
                foreach (var p in players)
                    p.gravity = lowGravityValue;
                ShowLabel("LOW GRAVITY");
                break;

            case GameModifier.KnockbackX1000:
                foreach (var p in players)
                    p.knockback = crazyKnockbackValue;
                ShowLabel("KNOCKBACK × 1000");
                break;

            case GameModifier.AcidColors:
                foreach (var p in players)
                {
                    var srP = p.GetComponent<SpriteRenderer>();
                    if (srP != null) srP.color = acidTint;
                }
                if (extraTintRenderers != null)
                    foreach (var r in extraTintRenderers)
                        if (r != null) r.color = acidTint;
                ShowLabel("ACID COLORS");
                break;

                case GameModifier.TinyArena:
                    if (arenaObjectsToScale == null || arenaObjectsToScale.Length == 0)
                    {
                        Debug.LogWarning("[Roulette] TINY ARENA: arenaObjectsToScale EMPTY");
                    }
                    else
                    {
                        if (tinyArenaRoutine != null)
                            StopCoroutine(tinyArenaRoutine);

                        tinyArenaRoutine = StartCoroutine(TinyArenaScaleRoutine());
                    }

                    ShowLabel("TINY ARENA");
                    break;


            case GameModifier.ReversedControls:
                ReverseControls = true;
                ShowLabel("REVERSED CONTROLS");
                break;
        }
    }

        private IEnumerator TinyArenaScaleRoutine()
    {
        if (arenaObjectsToScale == null || arenaObjectsToScale.Length == 0)
            yield break;


        float duration = 5f;

        Vector3[] originalScales = new Vector3[arenaObjectsToScale.Length];
        for (int i = 0; i < arenaObjectsToScale.Length; i++)
        {
            if (arenaObjectsToScale[i] == null) continue;
            originalScales[i] = arenaObjectsToScale[i].localScale;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);

            for (int i = 0; i < arenaObjectsToScale.Length; i++)
            {
                var tr = arenaObjectsToScale[i];
                if (tr == null) continue;


                Vector3 small = new Vector3(originalScales[i].x * tinyScale, originalScales[i].y, originalScales[i].z);
                tr.localScale = Vector3.Lerp(originalScales[i], small, lerp);
            }

            yield return null;
        }

        Debug.Log("[Roulette] TINY ARENA: reached tiny scale");

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);

            for (int i = 0; i < arenaObjectsToScale.Length; i++)
            {
                var tr = arenaObjectsToScale[i];
                if (tr == null) continue;

                Vector3 small = new Vector3(originalScales[i].x * tinyScale, originalScales[i].y, originalScales[i].z);
                tr.localScale = Vector3.Lerp(small, originalScales[i], lerp);

            }

            yield return null;
        }

        for (int i = 0; i < arenaObjectsToScale.Length; i++)
        {
            var tr = arenaObjectsToScale[i];
            if (tr == null) continue;

            tr.localScale = originalScales[i];
        }

        Debug.Log("[Roulette] TINY ARENA: restored original scale");
        tinyArenaRoutine = null;
    }


    void ShowLabel(string text)
    {
        if (modifierLabel == null) return;
        modifierLabel.text = text;
        modifierLabel.gameObject.SetActive(true);
        _labelTimer = labelShowTime;
    }

    void Update()
    {
        if (modifierLabel != null && _labelTimer > 0f)
        {
            _labelTimer -= Time.deltaTime;
            if (_labelTimer <= 0f)
                modifierLabel.gameObject.SetActive(false);
        }
    }
}

using System;
using System.Collections;
using Fusion;
using UnityEngine;

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
    public static event Action<string, float> OnShowModifierLabel;

    [SerializeField] float labelShowTime = 2f;

    [SerializeField] float defaultGravity = -50f;
    [SerializeField] float defaultKnockback = 14f;

    [SerializeField] float lowGravityValue = -15f;
    [SerializeField] float crazyKnockbackValue = 300f;

    [SerializeField] Color normalTint = Color.white;
    [SerializeField] Color acidPlayerTint = new Color(0.5f, 1f, 0.5f, 1f);
    [SerializeField] Color acidWorldTint = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] SpriteRenderer[] extraTintRenderers;

    [SerializeField] Transform[] arenaObjectsToScale;
    [SerializeField] float tinyScale = 0.5f;
    [SerializeField] float normalScale = 1f;

    Coroutine tinyArenaRoutine;
    Coroutine acidCameraRoutine;
    Coroutine acidSpritesRoutine;

    [Networked] public GameModifier CurrentModifier { get; set; }
    [Networked] TickTimer NextRollTimer { get; set; }

    Color[] worldOriginalColors;

    void Awake()
    {
        Instance = this;

        if (extraTintRenderers != null && extraTintRenderers.Length > 0)
        {
            worldOriginalColors = new Color[extraTintRenderers.Length];
            for (int i = 0; i < extraTintRenderers.Length; i++)
            {
                if (extraTintRenderers[i] != null)
                    worldOriginalColors[i] = extraTintRenderers[i].color;
            }
        }
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

        if (NextRollTimer.ExpiredOrNotRunning(Runner))
        {
            GameModifier rolled = Roll();
            RpcApplyModifier(rolled);
            NextRollTimer = TickTimer.CreateFromSeconds(Runner, 5f);
        }
    }

    GameModifier Roll()
    {
        Array values = Enum.GetValues(typeof(GameModifier));
        int i = UnityEngine.Random.Range(1, values.Length);
        GameModifier mod = (GameModifier)values.GetValue(i);
        return mod;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcApplyModifier(GameModifier modifier, RpcInfo info = default)
    {
        CurrentModifier = modifier;
        ApplyModifier(modifier);
    }

    void ApplyModifier()
    {
        ApplyModifier(CurrentModifier);
    }

    void ApplyModifier(GameModifier modifier)
    {
        ReverseControls = false;

        if (tinyArenaRoutine != null)
        {
            StopCoroutine(tinyArenaRoutine);
            tinyArenaRoutine = null;
        }

        if (acidCameraRoutine != null)
        {
            StopCoroutine(acidCameraRoutine);
            acidCameraRoutine = null;
        }

        if (acidSpritesRoutine != null)
        {
            StopCoroutine(acidSpritesRoutine);
            acidSpritesRoutine = null;
        }

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.gravity = defaultGravity;
            p.knockback = defaultKnockback;
            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = normalTint;
        }

        if (arenaObjectsToScale != null && arenaObjectsToScale.Length > 0)
        {
            foreach (var t in arenaObjectsToScale)
            {
                if (t != null)
                    t.localScale = Vector3.one * normalScale;
            }
        }

        if (extraTintRenderers != null && extraTintRenderers.Length > 0)
        {
            for (int i = 0; i < extraTintRenderers.Length; i++)
            {
                SpriteRenderer r = extraTintRenderers[i];
                if (r == null) continue;

                if (worldOriginalColors != null && worldOriginalColors.Length == extraTintRenderers.Length)
                    r.color = worldOriginalColors[i];
                else
                    r.color = normalTint;
            }
        }

        switch (modifier)
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
                    SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.color = acidPlayerTint;
                }

                if (extraTintRenderers != null && extraTintRenderers.Length > 0)
                {
                    foreach (var r in extraTintRenderers)
                    {
                        if (r != null)
                            r.color = acidWorldTint;
                    }
                }

                acidSpritesRoutine = StartCoroutine(AcidSpritesRoutine());
                acidCameraRoutine = StartCoroutine(AcidCameraRoutine());
                ShowLabel("ACID COLORS");
                break;

            case GameModifier.TinyArena:
                if (arenaObjectsToScale != null && arenaObjectsToScale.Length > 0)
                    tinyArenaRoutine = StartCoroutine(TinyArenaScaleRoutine());
                ShowLabel("TINY ARENA");
                break;

            case GameModifier.ReversedControls:
                ReverseControls = true;
                ShowLabel("REVERSED CONTROLS");
                break;

            case GameModifier.None:
                ShowLabel("NONE");
                break;
        }
    }

    IEnumerator TinyArenaScaleRoutine()
    {
        if (arenaObjectsToScale == null || arenaObjectsToScale.Length == 0)
        {
            tinyArenaRoutine = null;
            yield break;
        }

        float duration = 5f;
        Vector3[] originalScales = new Vector3[arenaObjectsToScale.Length];

        for (int i = 0; i < arenaObjectsToScale.Length; i++)
        {
            if (arenaObjectsToScale[i] != null)
                originalScales[i] = arenaObjectsToScale[i].localScale;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);

            for (int i = 0; i < arenaObjectsToScale.Length; i++)
            {
                Transform tr = arenaObjectsToScale[i];
                if (tr == null) continue;

                Vector3 small = new Vector3(originalScales[i].x * tinyScale, originalScales[i].y, originalScales[i].z);
                tr.localScale = Vector3.Lerp(originalScales[i], small, lerp);
            }

            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);

            for (int i = 0; i < arenaObjectsToScale.Length; i++)
            {
                Transform tr = arenaObjectsToScale[i];
                if (tr == null) continue;

                Vector3 small = new Vector3(originalScales[i].x * tinyScale, originalScales[i].y, originalScales[i].z);
                tr.localScale = Vector3.Lerp(small, originalScales[i], lerp);
            }

            yield return null;
        }

        for (int i = 0; i < arenaObjectsToScale.Length; i++)
        {
            Transform tr = arenaObjectsToScale[i];
            if (tr == null) continue;
            tr.localScale = originalScales[i];
        }

        tinyArenaRoutine = null;
    }

    IEnumerator AcidSpritesRoutine()
    {
        float t = 0f;

        while (true)
        {
            t += Time.deltaTime;
            float baseHue = Mathf.Repeat(t * 0.5f, 1f);

            PlayerController[] players = FindObjectsOfType<PlayerController>();
            for (int i = 0; i < players.Length; i++)
            {
                SpriteRenderer sr = players[i].GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                float h = Mathf.Repeat(baseHue + i * 0.13f, 1f);
                Color c = Color.HSVToRGB(h, 0.9f, 1f);
                sr.color = c;
            }

            if (extraTintRenderers != null && extraTintRenderers.Length > 0)
            {
                for (int i = 0; i < extraTintRenderers.Length; i++)
                {
                    SpriteRenderer r = extraTintRenderers[i];
                    if (r == null) continue;

                    float h = Mathf.Repeat(baseHue + 0.5f + i * 0.17f, 1f);
                    Color c = Color.HSVToRGB(h, 0.7f, 0.9f);
                    r.color = c;
                }
            }

            yield return null;
        }
    }

    IEnumerator AcidCameraRoutine()
    {
        float duration = 5f;
        Camera[] cams = Camera.allCameras;
        if (cams == null || cams.Length == 0)
        {
            acidCameraRoutine = null;
            yield break;
        }

        Color[] originalColors = new Color[cams.Length];
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null)
                originalColors[i] = cams[i].backgroundColor;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float hue = Mathf.Repeat(t * 0.6f, 1f);
            Color acidColor = Color.HSVToRGB(hue, 0.9f, 1f);

            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null)
                    cams[i].backgroundColor = acidColor;
            }

            yield return null;
        }

        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null)
                cams[i].backgroundColor = originalColors[i];
        }

        acidCameraRoutine = null;
    }

    void ShowLabel(string text)
    {
        OnShowModifierLabel?.Invoke(text, labelShowTime);
    }
}

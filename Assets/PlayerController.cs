using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    [Range(0f, 1f)] public float airControl = 0.6f;

    [Header("Jump / Gravity")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float gravity = -50f;
    public float maxFallSpeed = -25f;

    [Header("Ground check")]
    public LayerMask groundMask;
    public float groundCheckInset = 0.02f;

    [Header("Attack")]
    public float attackRadius = 0.75f;
    public float knockback = 30f;
    public LayerMask playerMask;

    [Header("Attack VFX")]
    public float slashDuration = 0.18f;
    public float slashVisualRadius = 0.9f;
    public float slashWidth = 0.07f;
    public Color slashColor = new Color(1f, 0.1f, 0.1f, 0.95f);
    public int slashSegments = 24;
    public Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
    public float hitFlashDuration = 0.1f;

    [Header("Sparks")]
    public Color sparksColor = new Color(1f, 0.2f, 0.2f, 1f);
    public float sparksLifetime = 0.25f;
    public int sparksBurstCount = 16;
    public float sparksSpeed = 5f;

    [Header("Camera Shake")]
    public float shakeIntensity = 0.2f;
    public float shakeDuration = 0.12f;
    public float shakeFreq = 40f;

    Collider2D _col;
    SpriteRenderer _sr;

    bool _isGrounded;
    float _timeSinceGrounded;
    float _timeSinceJumpPressed = 999f;

    float _horSpeed;
    float _vertSpeed;

    LineRenderer _slashLR;
    Coroutine _slashRoutine;
    ParticleSystem _sparksPS;

    public override void Spawned()
    {
        _col = GetComponent<Collider2D>();
        _sr  = GetComponent<SpriteRenderer>();

        SetupSlashRenderer();
        SetupSparks();

        Debug.Log($"[PlayerController-{Object.Id}] Spawned. StateAuth={Object.HasStateAuthority}, InputAuth={Object.HasInputAuthority}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!GetInput(out PlayerInputData input))
            return;

        float dt = Runner.DeltaTime;

        Vector2 move = input.Move;
        bool jumpPressed  = input.JumpPressed;
        bool attackPressed = input.AttackPressed;

        _timeSinceGrounded    += dt;
        _timeSinceJumpPressed += dt;

        if (jumpPressed)
            _timeSinceJumpPressed = 0f;

        if (_isGrounded)
            _timeSinceGrounded = 0f;

        bool canJump =
            jumpPressed &&
            (_isGrounded || _timeSinceGrounded <= coyoteTime) &&
            _timeSinceJumpPressed <= jumpBufferTime;

        if (canJump)
        {
            _vertSpeed = jumpForce;
            _isGrounded = false;
            _timeSinceJumpPressed = jumpBufferTime + 1f;
            _timeSinceGrounded    = coyoteTime + 1f;
        }

        _vertSpeed += gravity * dt;
        if (_vertSpeed < maxFallSpeed)
            _vertSpeed = maxFallSpeed;

        float targetX = move.x * moveSpeed;
        float control = _isGrounded ? 1f : airControl;
        _horSpeed = Mathf.Lerp(_horSpeed, targetX, control);

        Vector2 delta = new Vector2(_horSpeed, _vertSpeed) * dt;

        MoveAndCollide(ref delta);

        if (Mathf.Abs(_horSpeed) > 0.05f)
        {
            var s = transform.localScale;
            s.x = Mathf.Sign(_horSpeed) * Mathf.Abs(s.x);
            transform.localScale = s;
        }

        if (attackPressed)
            DoAttack();
    }

    void MoveAndCollide(ref Vector2 delta)
    {
        if (_col == null)
        {
            transform.position += (Vector3)delta;
            return;
        }

        float skin = groundCheckInset;


        Bounds b = _col.bounds;
        Vector2 center = b.center;
        Vector2 size   = b.size - new Vector3(skin * 2f, skin * 2f, 0f);


        if (Mathf.Abs(delta.x) > 0f)
        {
            Vector2 dir  = new Vector2(Mathf.Sign(delta.x), 0f);
            float   dist = Mathf.Abs(delta.x) + skin;

            var hit = Physics2D.BoxCast(center, size, 0f, dir, dist, groundMask);
            if (hit.collider != null)
            {
                float hitDist = hit.distance - skin;
                if (hitDist < 0f) hitDist = 0f;
                delta.x = hitDist * dir.x;
            }
        }


        if (Mathf.Abs(delta.y) > 0f)
        {
            Vector2 dir  = new Vector2(0f, Mathf.Sign(delta.y));
            float   dist = Mathf.Abs(delta.y) + skin;

            var hit = Physics2D.BoxCast(center, size, 0f, dir, dist, groundMask);
            if (hit.collider != null)
            {
                float hitDist = hit.distance - skin;
                if (hitDist < 0f) hitDist = 0f;
                delta.y = hitDist * dir.y;


                if (dir.y < 0f && _vertSpeed < 0f)
                    _vertSpeed = 0f;
                else if (dir.y > 0f && _vertSpeed > 0f)
                    _vertSpeed = 0f;
            }
        }


        transform.position += (Vector3)delta;

        b = _col.bounds;
        center = b.center;
        size   = b.size - new Vector3(skin * 2f, skin * 2f, 0f);

        float groundCheckDistance = skin * 2f; 
        var groundHit = Physics2D.BoxCast(center, size, 0f, Vector2.down, groundCheckDistance, groundMask);

        bool groundedNow = groundHit.collider != null && _vertSpeed <= 0.01f;
        _isGrounded = groundedNow;

        if (_isGrounded && _vertSpeed < 0f)
            _vertSpeed = 0f;
    }

    public void ApplyKnockbackLocal(Vector2 impulse)
    {
    _isGrounded = false;
    _timeSinceGrounded = coyoteTime + 1f; 
    _horSpeed += impulse.x;
    _vertSpeed += impulse.y;
    }

    void DoAttack()
    {
        float facing = Mathf.Sign(transform.localScale.x);
        Vector2 center = (Vector2)transform.position + new Vector2(facing * 5.6f, 0.2f);

        var hits = Physics2D.OverlapCircleAll(center, attackRadius);

        int hitCount = 0;

        foreach (var other in hits)
        {
            if (!other || other.gameObject == gameObject) continue;

            var victim = other.GetComponent<PlayerController>();
            if (victim && victim != this)
            {
                hitCount++;
                Vector2 dir = ((Vector2)victim.transform.position - (Vector2)transform.position).normalized;
                Vector2 impulse = dir * knockback;
                impulse.x *= 25f;
                if (impulse.y <= 0f) impulse.y = 8f;
                victim.ApplyKnockbackLocal(impulse);
                victim.RPC_PlayHitFlash();
            }
        }

        if (hitCount > 0)
        {
            RPC_PlayAttackVFX(center, facing);
            RPC_CameraShake(shakeIntensity, shakeDuration, shakeFreq);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_PlayAttackVFX(Vector2 center, float facing)
    {
        if (_slashRoutine != null)
            StopCoroutine(_slashRoutine);
        _slashRoutine = StartCoroutine(PlaySlashAndSparks(center, facing));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_PlayHitFlash()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(HitFlashRoutine());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    void RPC_CameraShake(float intensity, float duration, float freq)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(CameraShakeRoutine(intensity, duration, freq));
    }

    IEnumerator PlaySlashAndSparks(Vector2 center, float facing)
    {
        if (_slashLR == null) yield break;

        _slashLR.enabled = true;
        _slashLR.widthMultiplier = slashWidth;

        float arcDeg = 120f;
        float startDeg = -arcDeg * 0.5f;
        float endDeg   = arcDeg * 0.5f;

        _slashLR.positionCount = slashSegments + 1;

        float dirMul = Mathf.Sign(facing);
        Vector2 origin = center + new Vector2(0f, 0.05f);

        Color c = slashColor;
        SetLRColorAlpha(c.a);

        Vector3 tip = origin;
        for (int i = 0; i <= slashSegments; i++)
        {
            float t = i / (float)slashSegments;
            float ang = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
            float x = Mathf.Cos(ang) * slashVisualRadius * dirMul;
            float y = Mathf.Sin(ang) * slashVisualRadius * 0.7f;
            Vector3 p = origin + new Vector2(x, y);
            _slashLR.SetPosition(i, p);
            tip = p;
        }

        PlaySparks(tip);

        float tFade = 0f;
        while (tFade < slashDuration)
        {
            float a = Mathf.Lerp(c.a, 0f, tFade / slashDuration);
            SetLRColorAlpha(a);
            tFade += Time.deltaTime;
            yield return null;
        }

        _slashLR.enabled = false;
        _slashRoutine = null;
    }

    void SetLRColorAlpha(float a)
    {
        if (_slashLR == null) return;
        var sc = new Color(slashColor.r, slashColor.g, slashColor.b, a);
        _slashLR.startColor = sc;
        _slashLR.endColor   = sc;
        if (_slashLR.material != null)
            _slashLR.material.color = sc;
    }

    IEnumerator HitFlashRoutine()
    {
        if (_sr == null) yield break;

        Color original = _sr.color;
        _sr.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        _sr.color = original;
    }

    IEnumerator CameraShakeRoutine(float intensity, float duration, float freq)
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        Transform ct = cam.transform;
        Vector3 basePos = ct.localPosition;

        float t = 0f;
        while (t < duration)
        {
            float f = t * freq;
            float damper = 1f - (t / duration);
            Vector2 off = new Vector2(
                (Mathf.PerlinNoise(f, 0.5f) - 0.5f),
                (Mathf.PerlinNoise(0.5f, f) - 0.5f)
            );
            off += Random.insideUnitCircle * 0.15f;
            ct.localPosition = basePos + (Vector3)(off * intensity * damper);
            t += Time.deltaTime;
            yield return null;
        }

        ct.localPosition = basePos;
    }

    void SetupSlashRenderer()
    {
        var go = new GameObject("SlashVFX");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;

        _slashLR = go.AddComponent<LineRenderer>();
        _slashLR.enabled = false;
        _slashLR.useWorldSpace = true;
        _slashLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _slashLR.receiveShadows = false;
        _slashLR.numCapVertices = 4;
        _slashLR.numCornerVertices = 4;
        _slashLR.textureMode = LineTextureMode.Stretch;
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
            spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (spriteShader == null)
            spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (spriteShader == null)
        {
            Debug.LogError("[PlayerController] SlashVFX ERROR");
        }

        var mat = new Material(spriteShader);
        mat.color = slashColor;
        _slashLR.material = mat;

        _slashLR.widthMultiplier = slashWidth;
        _slashLR.sortingLayerID = _sr ? _sr.sortingLayerID : 0;
        _slashLR.sortingOrder   = (_sr ? _sr.sortingOrder : 0) + 1;
    }


    void SetupSparks()
    {
        var go = new GameObject("SparksVFX");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;

        _sparksPS = go.AddComponent<ParticleSystem>();

        var main = _sparksPS.main;
        main.duration = 0.3f;
        main.startLifetime = sparksLifetime;
        main.startSpeed = sparksSpeed;
        main.startSize = 0.08f;
        main.startColor = sparksColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 128;

        var emission = _sparksPS.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = _sparksPS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        shape.arc = 360f;

        var trails = _sparksPS.trails;
        trails.enabled = true;
        trails.inheritParticleColor = true;
        trails.ratio = 1f;

        var renderer = _sparksPS.GetComponent<ParticleSystemRenderer>();

        // >>> ZNOWU: szukamy shadera bezpiecznie
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
            spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (spriteShader == null)
            spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (spriteShader == null)
            Debug.LogError("[PlayerController] Nie znalazłem shadera do SparksVFX");

        renderer.material = new Material(spriteShader) { color = sparksColor };
        renderer.sortingLayerID = _sr ? _sr.sortingLayerID : 0;
        renderer.sortingOrder   = (_sr ? _sr.sortingOrder : 0) + 2;
    }


    void PlaySparks(Vector3 worldPos)
    {
        if (_sparksPS == null) return;

        _sparksPS.transform.position = worldPos;

        var emission = _sparksPS.emission;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)sparksBurstCount)
        });

        _sparksPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _sparksPS.Play();
    }

    public void OnKilled()
    {
        if (!Object.HasStateAuthority) return;

        _horSpeed = 0f;
        _vertSpeed = 0f;

        var spawn = FindFirstObjectByType<SpawnManager2D>();
        transform.position = spawn ? spawn.GetSpawnPoint() : Vector3.zero;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Vector2 center = (Vector2)transform.position + new Vector2(Mathf.Sign(transform.localScale.x) * 0.6f, 0.2f);
        Gizmos.DrawWireSphere(center, attackRadius);
    }
#endif
}

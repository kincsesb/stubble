using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FarmAnimalChaseSystem : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject chickenPrefab;
    public GameObject catPrefab;

    [Header("Circle Path")]
    [Tooltip("Auto-set from metal silo positions at Start. Override here as fallback.")]
    public Vector3 circleCenter = new Vector3(66f, 0f, -20f);
    [SerializeField] float circleRadius = 18f;

    [Header("Timing")]
    [Tooltip("Seconds before chaser/target roles reverse")]
    [SerializeField] float reverseAfterSeconds = 120f;
    [Tooltip("Seconds before everyone goes idle by the fence")]
    [SerializeField] float idleAfterSeconds    = 600f;
    [Tooltip("If true: cat chases chicken first. If false: chicken chases cat first.")]
    [SerializeField] bool catStartsChasing = true;

    [Header("Audio (optional)")]
    [SerializeField] AudioClip sfxRunLoop;
    [SerializeField] AudioClip sfxCatPurr;
    [SerializeField][Range(0f, 1f)] float sfxVolume = 0.06f;

    [Header("Capture Event")]
    [Tooltip("How many seconds the player must watch before capture triggers")]
    [SerializeField] float captureAfterWatchSeconds = 15f;
    [Tooltip("Disable to prevent capture from ever triggering")]
    [SerializeField] bool captureEnabled = true;

    // ---------------------------------------------------------------------------
    // Animation names
    // ---------------------------------------------------------------------------
    const string CAT_RUN_FAST = "Arm_Cat|RunFast_F_IP";
    const string CAT_RUN      = "Arm_Cat|Run_F_IP";
    const string CAT_IDLE     = "Arm_Cat|Idle_1";
    const string CAT_SIT      = "Arm_Cat|Sit_loop_1";
    const string CHK_RUN      = "Run";
    const string CHK_IDLE     = "Idle";

    const float CAT_CHASER_SPD   = 6.5f;
    const float CAT_TARGET_SPD   = 4.8f;
    const float CHICK_CHASER_SPD = 6.0f;
    const float CHICK_TARGET_SPD = 4.5f;

    // ---------------------------------------------------------------------------
    // Animal data
    // ---------------------------------------------------------------------------
    class Animal
    {
        public GameObject     go;
        public Animator       anim;
        public ParticleSystem dustPS;
        public float  angle;
        public float  speed;
        public bool   isChicken;
        public bool   isChaser;
        public Vector3 baseScale;
        public float  stepTime;
        public float  noiseSeed;
        public string playingAnim = "";
    }

    readonly List<Animal> _all = new();
    float _elapsed;
    int   _phase = -1;
    float _circleRadius;
    AudioSource _audioSrc;

    float _watchTimer;
    bool  _captureTriggered;
    bool  _captureRunning;

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    void Start()
    {
        _circleRadius = circleRadius;
        AutoLocateSilos();

        var chicken = FindAnimatedRoot("Chicken");
        var cat     = FindAnimatedRoot("CartoonCat_c3");
        if (chicken != null) AddAnimal(chicken, true,  playSpawn: false);
        if (cat     != null) AddAnimal(cat,     false, playSpawn: false);
        SpreadAngles();
        TriggerPhase(0);
        SetupAudio();
    }

    void AutoLocateSilos()
    {
        var positions = new List<Vector3>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.StartsWith("metal_silo_3d_model"))
            {
                positions.Add(go.transform.position);
                Debug.Log($"[FarmAnimalChaseSystem] Found silo '{go.name}' at {go.transform.position}");
            }
        }

        if (positions.Count == 0)
        {
            Debug.LogWarning("[FarmAnimalChaseSystem] No metal_silo_3d_model found — using Inspector circleCenter.");
            return;
        }

        Vector3 sum = Vector3.zero;
        foreach (var p in positions) sum += p;
        Vector3 centroid = sum / positions.Count;
        circleCenter = new Vector3(centroid.x, 0f, centroid.z);
        Debug.Log($"[FarmAnimalChaseSystem] Silo centroid → circleCenter = {circleCenter}");
    }

    void SetupAudio()
    {
        if (sfxRunLoop == null && sfxCatPurr == null) return;
        _audioSrc = gameObject.AddComponent<AudioSource>();
        _audioSrc.spatialBlend = 0f;
        _audioSrc.volume       = sfxVolume;
        _audioSrc.loop         = true;
        if (sfxRunLoop != null) { _audioSrc.clip = sfxRunLoop; _audioSrc.Play(); }
    }

    static GameObject FindAnimatedRoot(string name)
    {
        GameObject fallback = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name != name) continue;
            if (go.GetComponent<Animator>() != null) return go;
            fallback = go;
        }
        return fallback;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        int p = CurrentPhaseIndex();
        if (p != _phase) TriggerPhase(p);
        if (_phase == 2) return;
        if (_captureRunning) return;

        Tick();

        // Watch timer — only accumulates while player looks toward the animals
        if (captureEnabled && !_captureTriggered)
        {
            if (IsAnyAnimalVisible())
                _watchTimer += Time.deltaTime;
            else
                _watchTimer = Mathf.Max(0f, _watchTimer - Time.deltaTime * 0.5f);

            if (_watchTimer >= captureAfterWatchSeconds)
                TriggerCapture();
        }
    }

    // ---------------------------------------------------------------------------
    // Phase logic  (0 = first chase, 1 = reversed, 2 = idle)
    // ---------------------------------------------------------------------------

    int CurrentPhaseIndex()
    {
        if (_elapsed >= idleAfterSeconds)    return 2;
        if (_elapsed >= reverseAfterSeconds) return 1;
        return 0;
    }

    void TriggerPhase(int p)
    {
        _phase = p;
        StopAllCoroutines();

        if (p == 2)
        {
            DoIdlePhase();
            return;
        }

        // Phase 0: catStartsChasing determines roles.
        // Phase 1: roles flipped.
        bool catChases = (p == 0) ? catStartsChasing : !catStartsChasing;

        foreach (var a in _all)
        {
            a.isChaser = a.isChicken ? !catChases : catChases;
        }

        var targets = catChases ? Chickens() : Cats();
        var chasers = catChases ? Cats()     : Chickens();
        ClusterGroup(targets, 0f);
        ClusterGroup(chasers, -0.5f);
    }

    static void ClusterGroup(List<Animal> list, float baseAngle)
    {
        int n = list.Count;
        if (n == 0) return;
        float arc = n == 1 ? 0f : 0.45f;
        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0f : (float)i / (n - 1) - 0.5f;
            list[i].angle = Mathf.Repeat(baseAngle + t * arc, Mathf.PI * 2f);
        }
    }

    // ---------------------------------------------------------------------------
    // Animal management
    // ---------------------------------------------------------------------------

    void AddAnimal(GameObject go, bool isChicken, bool playSpawn = true)
    {
        var a = new Animal
        {
            go        = go,
            isChicken = isChicken,
            baseScale = go.transform.localScale,
            noiseSeed = Random.value * 100f,
        };
        a.anim   = go.GetComponent<Animator>()
                ?? go.GetComponentInParent<Animator>()
                ?? go.GetComponentInChildren<Animator>();
        a.dustPS = MakeDust(go);
        _all.Add(a);

        if (playSpawn) StartCoroutine(SpawnPunch(go, a.baseScale, a.dustPS));
    }

    IEnumerator SpawnPunch(GameObject go, Vector3 baseScale, ParticleSystem dustPS)
    {
        go.transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            if (go == null) yield break;
            t += Time.deltaTime / 0.38f;
            float s = t < 0.55f
                ? Mathf.SmoothStep(0f, 1.18f, t / 0.55f)
                : Mathf.SmoothStep(1.18f, 1f, (t - 0.55f) / 0.45f);
            go.transform.localScale = baseScale * s;
            yield return null;
        }
        go.transform.localScale = baseScale;
        if (dustPS != null) dustPS.Emit(18);
        if (_audioSrc != null && sfxCatPurr != null && !go.name.ToLower().Contains("chicken"))
            _audioSrc.PlayOneShot(sfxCatPurr, sfxVolume * 0.8f);
    }

    // ---------------------------------------------------------------------------
    // Running update (phases 0 & 1)
    // ---------------------------------------------------------------------------

    void Tick()
    {
        var chickens = Chickens();
        var cats     = Cats();

        foreach (var a in _all)
        {
            if (a.go == null) continue;

            float rawBase = BaseSpeed(a);
            float modSpd  = rawBase;

            if (a.isChaser)
            {
                var targets = a.isChicken ? cats : chickens;
                float gap = GapToNearest(a.angle, targets);
                float t   = Mathf.Clamp01(gap / (Mathf.PI * 0.5f));
                modSpd   *= Mathf.Lerp(0.82f, 1.45f, t);
            }
            else
            {
                var chasers = a.isChicken ? cats : chickens;
                float behind = GapBehind(a.angle, chasers);
                if (behind < 0.7f) modSpd *= Mathf.Lerp(1.55f, 1.0f, behind / 0.7f);
            }

            a.speed = modSpd;

            float angSpd = modSpd / _circleRadius;
            a.angle = Mathf.Repeat(a.angle + angSpd * Time.deltaTime, Mathf.PI * 2f);

            float noiseA = (Mathf.PerlinNoise(a.noiseSeed + _elapsed * 0.13f, 0f) - 0.5f) * 0.18f;
            float noiseR = (Mathf.PerlinNoise(a.noiseSeed + _elapsed * 0.17f, 5f) - 0.5f) * 1.4f;
            float dispAngle = a.angle + noiseA;
            float dispR     = _circleRadius + noiseR;

            Vector3 pos = circleCenter + new Vector3(Mathf.Cos(dispAngle), 0f, Mathf.Sin(dispAngle)) * dispR;
            pos.y = SampleTerrainHeight(pos.x, pos.z) + 0.05f;
            a.go.transform.position = pos;

            Vector3 tangent = new(-Mathf.Sin(dispAngle), 0f, Mathf.Cos(dispAngle));
            a.go.transform.rotation = Quaternion.LookRotation(tangent);

            a.stepTime += Time.deltaTime * modSpd * 2.8f;
            float pulse   = Mathf.Abs(Mathf.Sin(a.stepTime));
            float scaleY  = 1f + pulse * 0.12f;
            float scaleXZ = 1f - pulse * 0.06f;
            float bounceY = pulse * 0.045f;
            a.go.transform.localScale = new Vector3(
                a.baseScale.x * scaleXZ, a.baseScale.y * scaleY, a.baseScale.z * scaleXZ);
            a.go.transform.position += new Vector3(0, bounceY, 0);

            string animName = a.isChicken ? CHK_RUN : (modSpd > 6f ? CAT_RUN_FAST : CAT_RUN);
            PlayAnim(a, animName);
            if (a.anim) a.anim.speed = modSpd / rawBase;

            if (a.dustPS != null)
            {
                var em = a.dustPS.emission;
                em.rateOverTime = modSpd * 4.5f;
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Capture event
    // ---------------------------------------------------------------------------

    bool IsAnyAnimalVisible()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        foreach (var a in _all)
        {
            if (a.go == null) continue;
            if (GeometryUtility.TestPlanesAABB(planes, new Bounds(a.go.transform.position, Vector3.one * 3f)))
                return true;
        }
        return false;
    }

    void TriggerCapture()
    {
        _captureTriggered = true;
        var cats     = Cats();
        var chickens = Chickens();
        if (cats.Count == 0 || chickens.Count == 0) return;

        var cat     = cats[0];
        var chicken = chickens[0];
        _captureRunning = true;
        StartCoroutine(PlayCapture(cat, chicken));
    }

    IEnumerator PlayCapture(Animal cat, Animal chicken)
    {
        // --- 1. Cat rushes toward chicken ---
        Vector3 chickenPos = chicken.go.transform.position;
        Vector3 catStart   = cat.go.transform.position;
        float   t          = 0f;
        while (t < 1f && cat.go != null && chicken.go != null)
        {
            t += Time.deltaTime / 0.45f;
            cat.go.transform.position = Vector3.Lerp(catStart, chickenPos, Mathf.SmoothStep(0f, 1f, t));
            cat.go.transform.LookAt(chicken.go.transform.position);
            yield return null;
        }

        if (cat.go == null || chicken.go == null) { _captureRunning = false; yield break; }

        // --- 2. Attack loop (3 hits) ---
        Vector3 capturePos = chicken.go.transform.position;

        for (int hit = 0; hit < 3; hit++)
        {
            // Attack animation
            string attackAnim = hit < 2 ? "Arm_Cat|Attack_F" : "Arm_Cat|Attack_Series";
            PlayAnim(cat, attackAnim);
            cat.playingAnim = ""; // allow re-trigger next iteration

            // Dust burst + freeze frame
            EmitCaptureDust(capturePos, hit == 2);
            yield return StartCoroutine(FreezeFrame(hit == 2 ? 0.09f : 0.05f));

            // Feel_FullHit on final strike
            if (hit == 2) TriggerFeelHit();

            // Camera shake
            StartCoroutine(ShakeMainCamera(hit == 2 ? 0.25f : 0.12f, hit == 2 ? 0.18f : 0.06f));

            yield return new WaitForSeconds(0.38f);
        }

        // --- 3. Chicken death & cat run-away ---
        StartCoroutine(ChickenDeath(chicken));
        StartCoroutine(CatRunAway(cat));
        yield return new WaitForSeconds(2.5f);
        _captureRunning = false;
    }

    IEnumerator FreezeFrame(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    void TriggerFeelHit()
    {
        // Try the pre-configured Feel_FullHit MMF_Player in the scene
        var feelGo = GameObject.Find("Feel_FullHit");
        if (feelGo == null) return;
        var player = feelGo.GetComponent<MoreMountains.Feedbacks.MMF_Player>();
        player?.PlayFeedbacks(feelGo.transform.position);
    }

    IEnumerator ShakeMainCamera(float duration, float magnitude)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        Vector3 origin = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - elapsed / duration;
            cam.transform.localPosition = origin + (Vector3)Random.insideUnitCircle * magnitude * fade;
            yield return null;
        }
        cam.transform.localPosition = origin;
    }

    void EmitCaptureDust(Vector3 pos, bool big)
    {
        int count  = big ? 60 : 25;
        float size = big ? 0.8f : 0.35f;

        var psGo = new GameObject("_CaptureDust");
        psGo.transform.position = pos + Vector3.up * 0.3f;
        var ps   = psGo.AddComponent<ParticleSystem>();

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetColor("_BaseColor", new Color(0.85f, 0.75f, 0.55f, 0.9f));
        rend.material = mat;

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, big ? 1.4f : 0.9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, big ? 5f : 3f);
        main.startSize       = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.5f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.8f, 0.6f, 0.9f), new Color(1f, 0.95f, 0.8f, 0.5f));
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop            = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = big ? 0.8f : 0.4f;

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOL.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Emit(count);
        Destroy(psGo, 2.5f);
    }

    IEnumerator ChickenDeath(Animal chicken)
    {
        if (chicken.go == null) yield break;
        _all.Remove(chicken);

        if (chicken.dustPS != null) { chicken.dustPS.emission.enabled = false; chicken.dustPS.Stop(); }

        // Theatrical: giant dust explosion, chicken vanishes instantly
        EmitCaptureDust(chicken.go.transform.position, big: true);
        EmitCaptureDust(chicken.go.transform.position + Vector3.up * 0.4f, big: true);
        Destroy(chicken.go);
    }

    IEnumerator CatRunAway(Animal cat)
    {
        if (cat.go == null) yield break;
        _all.Remove(cat);

        if (cat.dustPS != null)
        {
            var em = cat.dustPS.emission;
            em.rateOverTime = 0f;
            cat.dustPS.Stop();
        }

        // --- Sit triumphantly ---
        PlayAnim(cat, CAT_SIT);
        if (cat.anim) cat.anim.speed = 1f;
        yield return new WaitForSeconds(1.0f);

        // --- Roll / lie belly ---
        if (cat.go == null) yield break;
        PlayAnim(cat, "Arm_Cat|Trans_Sit_LieBelly");
        yield return new WaitForSeconds(0.5f);
        PlayAnim(cat, "Arm_Cat|Lie_belly_loop_2");
        yield return new WaitForSeconds(1.2f);

        // --- Get up ---
        if (cat.go == null) yield break;
        PlayAnim(cat, "Arm_Cat|Lie_belly_end");
        yield return new WaitForSeconds(0.5f);

        // --- Sprint away ---
        if (cat.go == null) yield break;
        PlayAnim(cat, CAT_RUN_FAST);
        if (cat.anim) cat.anim.speed = 1.6f;

        Vector3 awayDir = (cat.go.transform.position - circleCenter).normalized;
        awayDir.y = 0f;
        if (awayDir == Vector3.zero) awayDir = Vector3.forward;

        if (cat.dustPS != null)
        {
            cat.dustPS.Play();
            var em2 = cat.dustPS.emission;
            em2.rateOverTime = 35f;
        }

        float elapsed = 0f;
        while (elapsed < 4f && cat.go != null)
        {
            elapsed += Time.deltaTime;
            cat.go.transform.position += awayDir * 9f * Time.deltaTime;
            cat.go.transform.rotation  = Quaternion.LookRotation(awayDir);
            float shrink = Mathf.Clamp01(1f - elapsed / 3.5f);
            cat.go.transform.localScale = cat.baseScale * shrink;
            yield return null;
        }
        if (cat.go != null) Destroy(cat.go);
    }

    // ---------------------------------------------------------------------------
    // Idle phase — animals line up at the south edge of the circle, facing inward
    // Cats sit, chickens stand.
    // ---------------------------------------------------------------------------

    void DoIdlePhase()
    {
        int   total   = _all.Count;
        float spacing = 1.3f;
        float startX  = circleCenter.x - (total - 1) * spacing * 0.5f;

        // South edge of circle, just outside it — looking north toward the field
        float idleZ = circleCenter.z - _circleRadius - 1.5f;

        // Face inward = toward circleCenter from idle line
        Vector3 lookDir = new Vector3(0f, 0f, 1f); // +Z = into field
        Quaternion idleLook = Quaternion.LookRotation(lookDir);

        for (int i = 0; i < total; i++)
        {
            var a = _all[i];
            if (a.go == null) continue;

            float groundY = SampleTerrainHeight(startX + i * spacing, idleZ);
            Vector3 dest = new(startX + i * spacing, groundY + 0.05f, idleZ);
            StartCoroutine(SlideToPos(a.go, dest, idleLook, 2.5f));

            a.go.transform.localScale = a.baseScale;

            // Cats sit, chickens stand
            string idleAnim = a.isChicken ? CHK_IDLE : CAT_SIT;
            PlayAnim(a, idleAnim);
            if (a.anim) a.anim.speed = 1f;

            if (a.dustPS != null)
            {
                var em = a.dustPS.emission;
                em.rateOverTime = 0f;
                a.dustPS.Stop();
            }
        }

        if (_audioSrc != null) _audioSrc.volume = 0f;
    }

    IEnumerator SlideToPos(GameObject go, Vector3 dest, Quaternion rot, float dur)
    {
        if (go == null) yield break;
        Vector3 start = go.transform.position;
        float t = 0f;
        while (t < 1f)
        {
            if (go == null) yield break;
            t += Time.deltaTime / dur;
            go.transform.position = Vector3.Lerp(start, dest, Mathf.SmoothStep(0f, 1f, t));
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, rot, t);
            yield return null;
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    float BaseSpeed(Animal a) => a.isChicken
        ? (a.isChaser ? CHICK_CHASER_SPD : CHICK_TARGET_SPD)
        : (a.isChaser ? CAT_CHASER_SPD   : CAT_TARGET_SPD);

    float GapToNearest(float from, List<Animal> targets)
    {
        float min = float.MaxValue;
        foreach (var t in targets)
        {
            float g = Mathf.Repeat(t.angle - from + Mathf.PI * 2f, Mathf.PI * 2f);
            if (g < min) min = g;
        }
        return min > 1e6f ? Mathf.PI : min;
    }

    float GapBehind(float targetAngle, List<Animal> chasers)
    {
        float min = float.MaxValue;
        foreach (var c in chasers)
        {
            float g = Mathf.Repeat(targetAngle - c.angle + Mathf.PI * 2f, Mathf.PI * 2f);
            if (g < min) min = g;
        }
        return min > 1e6f ? Mathf.PI * 2f : min;
    }

    void PlayAnim(Animal a, string name)
    {
        if (a.anim == null || a.playingAnim == name) return;
        a.playingAnim = name;
        a.anim.CrossFadeInFixedTime(name, 0.2f);
    }

    void SpreadAngles()
    {
        int n = _all.Count;
        for (int i = 0; i < n; i++)
            _all[i].angle = Mathf.PI * 2f * i / Mathf.Max(n, 1);
    }

    List<Animal> Chickens() => _all.FindAll(a =>  a.isChicken && a.go != null && a.go.activeSelf);
    List<Animal> Cats()     => _all.FindAll(a => !a.isChicken && a.go != null && a.go.activeSelf);

    static float SampleTerrainHeight(float x, float z)
    {
        var worldPos = new Vector3(x, 0f, z);
        foreach (var terrain in Terrain.activeTerrains)
        {
            var tp = terrain.transform.position;
            var sz = terrain.terrainData.size;
            if (x >= tp.x && x <= tp.x + sz.x && z >= tp.z && z <= tp.z + sz.z)
                return terrain.SampleHeight(worldPos) + tp.y;
        }
        return 0f;
    }

    // ---------------------------------------------------------------------------
    // Dust particle system
    // ---------------------------------------------------------------------------

    static ParticleSystem MakeDust(GameObject parent)
    {
        var psGo = new GameObject("_DustPS");
        psGo.transform.SetParent(parent.transform);
        psGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);

        var ps = psGo.AddComponent<ParticleSystem>();

        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        var dustMat    = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        dustMat.SetColor("_BaseColor", new Color(0.82f, 0.72f, 0.52f, 0.7f));
        psRenderer.material = dustMat;

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.25f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.82f, 0.72f, 0.52f, 0.7f),
                                   new Color(0.97f, 0.94f, 0.84f, 0.4f));
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 60;
        main.gravityModifier = 0.65f;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.2f;
        shape.rotation  = new Vector3(90f, 0f, 0f);

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOL.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size    = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.9f));

        ps.Play();
        return ps;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FarmAnimalChaseSystem : MonoBehaviour
{
    [Header("Prefabs (spawning extras)")]
    public GameObject chickenPrefab;
    public GameObject catPrefab;

    [Header("Circle Path")]
    [Tooltip("Fallback center if no metal silos found in scene")]
    public Vector3 circleCenter = new Vector3(66f, 0f, -20f);

    [Header("Audio (optional — drag clips from SFX / Feel folders)")]
    [SerializeField] AudioClip sfxRunLoop;
    [SerializeField] AudioClip sfxCatPurr;
    [SerializeField][Range(0f, 1f)] float sfxVolume = 0.06f;

    // ---------------------------------------------------------------------------
    // Phase table — editable in Inspector
    // ---------------------------------------------------------------------------
    [System.Serializable]
    public class PhaseEntry
    {
        [Tooltip("Seconds from session start when this phase activates")]
        public float startTime;

        [Tooltip("How many chickens should be active (-1 = keep current count)")]
        public int numChickens = 1;

        [Tooltip("How many cats should be active (-1 = keep current count)")]
        public int numCats = 1;

        [Tooltip("If true: cats chase chickens. If false: chickens chase cats.")]
        public bool catIsChaser = true;

        [Tooltip("If true: all animals stop chasing and idle facing the field")]
        public bool idle;

        [TextArea(1, 2)]
        [Tooltip("Readable description shown in Inspector — no effect at runtime")]
        public string description;
    }

    [Header("Phase Timeline")]
    [SerializeField] PhaseEntry[] phases =
    {
        new() { startTime =    0, numChickens = 1,  numCats = 1,  catIsChaser = true,  idle = false, description = "Cat chases chicken" },
        new() { startTime =  120, numChickens = 1,  numCats = 1,  catIsChaser = false, idle = false, description = "Chicken chases cat" },
        new() { startTime =  600, numChickens = 1,  numCats = 1,  catIsChaser = true,  idle = false, description = "Cat chases chicken again" },
        new() { startTime = 1800, numChickens = 3,  numCats = 1,  catIsChaser = false, idle = false, description = "3 chickens chase 1 cat" },
        new() { startTime = 3600, numChickens = 3,  numCats = 4,  catIsChaser = true,  idle = false, description = "4 cats chase 3 chickens" },
        new() { startTime = 7200, numChickens = 20, numCats = 4,  catIsChaser = false, idle = false, description = "20 chickens chase 4 cats" },
        new() { startTime = 9900, numChickens = -1, numCats = -1, catIsChaser = false, idle = true,  description = "All idle — animals watch the field" },
    };

    // ---------------------------------------------------------------------------
    // Animation names
    // ---------------------------------------------------------------------------
    const string CAT_RUN_FAST = "Arm_Cat|RunFast_F_IP";
    const string CAT_RUN      = "Arm_Cat|Run_F_IP";
    const string CAT_IDLE     = "Arm_Cat|Idle_1";
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
        public float  noiseSeed;   // per-animal Perlin offset
        public string playingAnim = "";
    }

    readonly List<Animal> _all = new();
    float _elapsed;
    int   _phase = -1;
    float _circleRadius = 18f;
    AudioSource _audioSrc;

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    void Start()
    {
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
        var siloPositions = new List<Vector3>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.ToLower().Contains("silo") && go.transform.childCount > 0)
                siloPositions.Add(go.transform.position);
        }
        if (siloPositions.Count >= 2)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in siloPositions) sum += p;
            var centroid = sum / siloPositions.Count;
            // Keep Y=0, only use XZ centroid
            circleCenter = new Vector3(centroid.x, 0f, centroid.z);
        }
    }

    void SetupAudio()
    {
        if (sfxRunLoop == null && sfxCatPurr == null) return;
        _audioSrc = gameObject.AddComponent<AudioSource>();
        _audioSrc.spatialBlend = 0f;
        _audioSrc.volume       = sfxVolume;
        _audioSrc.loop         = true;
        if (sfxRunLoop != null)
        {
            _audioSrc.clip = sfxRunLoop;
            _audioSrc.Play();
        }
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
        if (_phase >= 0 && phases[_phase].idle) return;
        Tick();
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

        if (playSpawn)
            StartCoroutine(SpawnPunch(go, a.baseScale, a.dustPS));
    }

    void SpawnAnimal(bool isChicken)
    {
        var prefab = isChicken ? chickenPrefab : catPrefab;
        if (prefab == null) return;
        float angle = Random.value * Mathf.PI * 2f;
        Vector3 pos = CirclePos(angle);
        pos.y       = SampleTerrainHeight(pos.x, pos.z) + 0.05f;
        var go      = Instantiate(prefab, pos, Quaternion.identity);
        go.name     = isChicken ? $"Chicken_{_all.Count}" : $"Cat_{_all.Count}";
        AddAnimal(go, isChicken, playSpawn: true);
        _all[^1].angle = angle;
    }

    IEnumerator SpawnPunch(GameObject go, Vector3 baseScale, ParticleSystem dustPS)
    {
        // Scale in: 0 → 1.18 → 1.0
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

        // Dust burst on land
        if (dustPS != null)
        {
            var burst = new ParticleSystem.Burst(0f, 18);
            var em = dustPS.emission;
            em.SetBurst(0, burst);
            dustPS.Emit(18);
        }

        // Cat purr on cat spawn
        if (_audioSrc != null && sfxCatPurr != null && !go.name.ToLower().Contains("chicken"))
            _audioSrc.PlayOneShot(sfxCatPurr, sfxVolume * 0.8f);
    }

    // ---------------------------------------------------------------------------
    // Phase transitions
    // ---------------------------------------------------------------------------

    int CurrentPhaseIndex()
    {
        for (int i = phases.Length - 1; i >= 0; i--)
            if (_elapsed >= phases[i].startTime) return i;
        return 0;
    }

    void TriggerPhase(int p)
    {
        _phase = p;
        StopAllCoroutines();
        StartCoroutine(RunPhase(p));
    }

    IEnumerator RunPhase(int p)
    {
        var cfg = phases[p];
        if (cfg.idle) { DoIdlePhase(); yield break; }

        while (cfg.numChickens > 0 && Chickens().Count < cfg.numChickens)
        {
            if (chickenPrefab) SpawnAnimal(true);
            yield return new WaitForSeconds(0.35f);
        }
        while (cfg.numCats > 0 && Cats().Count < cfg.numCats)
        {
            if (catPrefab) SpawnAnimal(false);
            yield return new WaitForSeconds(0.35f);
        }

        foreach (var a in Chickens()) a.isChaser = !cfg.catIsChaser;
        foreach (var a in Cats())     a.isChaser =  cfg.catIsChaser;

        _circleRadius = Mathf.Max(18f, _all.Count * 1.8f);

        var targets = cfg.catIsChaser ? Chickens() : Cats();
        var chasers = cfg.catIsChaser ? Cats()     : Chickens();

        // Targets clustered at 0, chasers clustered just behind at -0.5 rad
        ClusterGroup(targets, 0f);
        ClusterGroup(chasers, -0.5f);
    }

    // Place all animals in a tight arc centred on baseAngle.
    // Single animal → just at baseAngle. 2+ → 0.4 rad total arc.
    static void ClusterGroup(List<Animal> list, float baseAngle)
    {
        int n = list.Count;
        if (n == 0) return;
        float arc = n == 1 ? 0f : Mathf.Min(0.45f, 0.45f);
        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0f : (float)i / (n - 1) - 0.5f;
            list[i].angle = Mathf.Repeat(baseAngle + t * arc, Mathf.PI * 2f);
        }
    }

    // ---------------------------------------------------------------------------
    // Running update
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
                float closeBehind = GapBehind(a.angle, chasers);
                if (closeBehind < 0.7f)
                    modSpd *= Mathf.Lerp(1.55f, 1.0f, closeBehind / 0.7f);
            }

            a.speed = modSpd;

            // Advance on circle
            float angSpd = modSpd / _circleRadius;
            a.angle = Mathf.Repeat(a.angle + angSpd * Time.deltaTime, Mathf.PI * 2f);

            // Wobble: slight Perlin noise on angle and radius so it's not robotically circular
            float noiseA = (Mathf.PerlinNoise(a.noiseSeed + _elapsed * 0.13f, 0f) - 0.5f) * 0.18f;
            float noiseR = (Mathf.PerlinNoise(a.noiseSeed + _elapsed * 0.17f, 5f) - 0.5f) * 1.4f;
            float displayAngle = a.angle + noiseA;
            float displayR     = _circleRadius + noiseR;

            // Position — sample terrain so animals don't float or sink
            Vector3 pos = circleCenter + new Vector3(
                Mathf.Cos(displayAngle), 0f, Mathf.Sin(displayAngle)) * displayR;
            pos.y = SampleTerrainHeight(pos.x, pos.z) + 0.05f;
            a.go.transform.position = pos;

            // Facing — tangent of display angle
            Vector3 tangent = new(-Mathf.Sin(displayAngle), 0f, Mathf.Cos(displayAngle));
            a.go.transform.rotation = Quaternion.LookRotation(tangent);

            // Cartoon squash/stretch
            a.stepTime += Time.deltaTime * modSpd * 2.8f;
            float pulse   = Mathf.Abs(Mathf.Sin(a.stepTime));
            float scaleY  = 1f + pulse * 0.12f;
            float scaleXZ = 1f - pulse * 0.06f;
            float bounceY = pulse * 0.045f;
            a.go.transform.localScale = new Vector3(
                a.baseScale.x * scaleXZ,
                a.baseScale.y * scaleY,
                a.baseScale.z * scaleXZ);
            a.go.transform.position += new Vector3(0, bounceY, 0);

            // Animation
            string animName = a.isChicken
                ? CHK_RUN
                : (modSpd > 6f ? CAT_RUN_FAST : CAT_RUN);
            PlayAnim(a, animName);
            if (a.anim) a.anim.speed = modSpd / rawBase;

            // Dust
            if (a.dustPS != null)
            {
                var em = a.dustPS.emission;
                em.rateOverTime = modSpd * 4.5f;
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Idle phase
    // ---------------------------------------------------------------------------

    void DoIdlePhase()
    {
        int   total   = _all.Count;
        float spacing = 1.3f;
        float lineZ   = -3.5f;
        float startX  = circleCenter.x - (total - 1) * spacing * 0.5f;

        Quaternion idleLook = Quaternion.LookRotation(new Vector3(0f, -0.25f, 1f).normalized);

        for (int i = 0; i < total; i++)
        {
            var a = _all[i];
            if (a.go == null) continue;

            Vector3 dest = new(startX + i * spacing, a.go.transform.position.y, lineZ);
            StartCoroutine(SlideToPos(a.go, dest, idleLook, 2.5f));

            a.go.transform.localScale = a.baseScale;

            string idle = a.isChicken ? CHK_IDLE : CAT_IDLE;
            PlayAnim(a, idle);
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
        float   t     = 0f;
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

    Vector3 CirclePos(float angle) =>
        circleCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _circleRadius;

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
        a.anim.CrossFadeInFixedTime(name, 0.15f);
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

using UnityEngine;

/// <summary>
/// Attach to any GameObject to drive the grass trampling shader from that position.
/// Walks in a circle so the effect is easy to observe.
/// </summary>
public class GrassTrampleTester : MonoBehaviour
{
    [Header("Circle walk")]
    public float radius = 3f;
    public float speed  = 0.6f;   // radians per second

    float _angle;
    Vector3 _center;

    void Start()
    {
        _center = transform.position;
        _angle  = 0f;
    }

    void Update()
    {
        _angle += speed * Time.deltaTime;
        transform.position = _center + new Vector3(
            Mathf.Cos(_angle) * radius,
            0f,
            Mathf.Sin(_angle) * radius);

        Shader.SetGlobalVector("_PlayerFeetWS", transform.position);
    }
}
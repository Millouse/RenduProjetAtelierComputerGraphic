using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraRail : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("World-space positions the camera will travel through.")]
    public Transform[] waypoints;

    [Header("Movement")]
    [Range(0.5f, 50f)]
    public float speed = 5f;

    [Tooltip("Ease in / ease out strength (0 = linear, 1 = full ease).")]
    [Range(0f, 1f)]
    public float easing = 0.5f;

    [Header("Rotation")]
    [Tooltip("If set, the camera will always look at this object.")]
    public Transform lookAtTarget;

    [Tooltip("How fast the camera rotates to face the target or the path direction.")]
    public float rotationSpeed = 3f;

    [Header("Playback")]
    public bool playOnStart = false;
    public bool loop = false;
    
    private float _progress = 0f;   // 0..1 along the full path
    private bool  _playing  = false;
    private float _totalLength;
    
    public string sceneToLoadOnEnd = "";
    

    private void Start()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning("[CameraRail] Need at least 2 waypoints.");
            return;
        }

        _totalLength = EstimatePathLength();

        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!_playing || waypoints == null || waypoints.Length < 2)
            return;

        // Advance progress
        _progress += (speed / _totalLength) * Time.deltaTime;

        if (_progress >= 1f)
        {
            _progress = loop ? 0f : 1f;
            if (!loop)
            {
                _playing = false;
                if (SceneTransitionManager.Instance != null)
                    SceneTransitionManager.Instance.LoadScene(sceneToLoadOnEnd);
                else
                {
                    if (!string.IsNullOrEmpty(sceneToLoadOnEnd))
                        SceneManager.LoadScene(sceneToLoadOnEnd);
                }
            }
            
        }
        
        float t = ApplyEasing(_progress);
        
        Vector3 targetPos = SampleCatmullRom(t);
        transform.position = targetPos;
        
        if (lookAtTarget != null)
        {
            Quaternion desired = Quaternion.LookRotation(lookAtTarget.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);
        }
        else
        {
            float tAhead = Mathf.Clamp01(_progress + 0.01f);
            Vector3 ahead = SampleCatmullRom(tAhead);
            Vector3 dir = ahead - targetPos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);
            }
        }
    }
    

    public void Play()
    {
        _progress = 0f;
        _playing  = true;
    }

    public void Pause()  => _playing = false;
    public void Resume() => _playing = true;

    public void Stop()
    {
        _playing  = false;
        _progress = 0f;
    }

    
    public IEnumerator PlayAndWait()
    {
        Play();
        while (_playing) yield return null;
    }

    
    private Vector3 SampleCatmullRom(float t)
    {
        int count = waypoints.Length;
        float scaled = t * (count - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, count - 2);
        float localT = scaled - i;

        Vector3 p0 = waypoints[Mathf.Max(i - 1, 0)].position;
        Vector3 p1 = waypoints[i].position;
        Vector3 p2 = waypoints[Mathf.Min(i + 1, count - 1)].position;
        Vector3 p3 = waypoints[Mathf.Min(i + 2, count - 1)].position;

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private float ApplyEasing(float t)
    {
    
        float smooth = t * t * (3f - 2f * t);
        return Mathf.Lerp(t, smooth, easing);
    }

    
    private float EstimatePathLength(int samples = 200)
    {
        float len = 0f;
        Vector3 prev = SampleCatmullRom(0f);
        for (int i = 1; i <= samples; i++)
        {
            Vector3 next = SampleCatmullRom(i / (float)samples);
            len += Vector3.Distance(prev, next);
            prev = next;
        }
        return Mathf.Max(len, 0.001f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        int steps = 80;
        Vector3 prev = SampleCatmullRom(0f);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = SampleCatmullRom(i / (float)steps);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        // Draw waypoint spheres
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        foreach (var wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawSphere(wp.position, 0.15f);
        }

        // Draw look-at target
        if (lookAtTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lookAtTarget.position, 0.3f);
        }
    }
#endif
}
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

    [Header("Teleport Segment")]
    [Tooltip("If enabled, the camera will instantly teleport from teleportFromWaypoint to teleportToWaypoint.")]
    public bool useTeleportSegment = true;

    [Tooltip("Index of the waypoint where the teleport starts.")]
    public int teleportFromWaypoint = 2;

    [Tooltip("Index of the waypoint where the teleport ends.")]
    public int teleportToWaypoint = 3;

    public string sceneToLoadOnEnd = "";

    private float _progress = 0f;
    private bool _playing = false;
    private float _totalLength;
    private bool _teleportDone = false;

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

        _progress += (speed / _totalLength) * Time.deltaTime;

        if (useTeleportSegment && !_teleportDone && IsTeleportSegmentValid())
        {
            int segmentCount = waypoints.Length - 1;
            float teleportStartT = teleportFromWaypoint / (float)segmentCount;
            float teleportEndT = teleportToWaypoint / (float)segmentCount;

            if (_progress >= teleportStartT)
            {
                _progress = teleportEndT;
                _teleportDone = true;

                // Snap direct au waypoint d'arrivée
                transform.position = waypoints[teleportToWaypoint].position;

                if (lookAtTarget != null)
                {
                    transform.rotation = Quaternion.LookRotation(lookAtTarget.position - transform.position);
                }
                else if (teleportToWaypoint + 1 < waypoints.Length)
                {
                    Vector3 dir = (waypoints[teleportToWaypoint + 1].position - transform.position).normalized;
                    if (dir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        if (_progress >= 1f)
        {
            _progress = loop ? 0f : 1f;

            if (loop)
            {
                _teleportDone = false;
            }
            else
            {
                _playing = false;

                if (SceneTransitionManager.Instance != null)
                    SceneTransitionManager.Instance.LoadScene(sceneToLoadOnEnd);
                else if (!string.IsNullOrEmpty(sceneToLoadOnEnd))
                    SceneManager.LoadScene(sceneToLoadOnEnd);

                return;
            }
        }

        float t = ApplyEasing(_progress);
        Vector3 targetPos = SampleCustomPath(t);
        transform.position = targetPos;

        if (lookAtTarget != null)
        {
            Quaternion desired = Quaternion.LookRotation(lookAtTarget.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);
        }
        else
        {
            float tAhead = Mathf.Clamp01(_progress + 0.01f);
            Vector3 ahead = SampleCustomPath(ApplyEasing(tAhead));
            Vector3 dir = ahead - targetPos;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void Play()
    {
        _progress = 0f;
        _playing = true;
        _teleportDone = false;
    }

    public void Pause() => _playing = false;
    public void Resume() => _playing = true;

    public void Stop()
    {
        _playing = false;
        _progress = 0f;
        _teleportDone = false;
    }

    public IEnumerator PlayAndWait()
    {
        Play();
        while (_playing)
            yield return null;
    }

    private bool IsTeleportSegmentValid()
    {
        if (waypoints == null || waypoints.Length < 2)
            return false;

        if (teleportFromWaypoint < 0 || teleportFromWaypoint >= waypoints.Length - 1)
            return false;

        if (teleportToWaypoint != teleportFromWaypoint + 1)
            return false;

        if (teleportToWaypoint >= waypoints.Length)
            return false;

        return true;
    }

    private Vector3 SampleCustomPath(float t)
    {
        int count = waypoints.Length;
        int segmentCount = count - 1;

        float scaled = t * segmentCount;
        int currentSegment = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, segmentCount - 1);
        float localT = scaled - currentSegment;

        if (!useTeleportSegment || !IsTeleportSegmentValid())
            return SampleCatmullRom(t);

        int beforeSegment = teleportFromWaypoint - 1;
        int teleportSegment = teleportFromWaypoint;
        int afterSegment = teleportToWaypoint;

        // Segment avant téléport = ligne droite
        if (currentSegment == beforeSegment && beforeSegment >= 0)
        {
            return Vector3.Lerp(
                waypoints[beforeSegment].position,
                waypoints[beforeSegment + 1].position,
                localT
            );
        }

        // Segment téléporté = position instantanée sur le point d'arrivée
        if (currentSegment == teleportSegment)
        {
            return waypoints[teleportToWaypoint].position;
        }

        return SampleCatmullRom(t);
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

    private float EstimatePathLength(int samples = 300)
    {
        float len = 0f;
        Vector3 prev = SampleCustomPath(0f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 next = SampleCustomPath(t);
            len += Vector3.Distance(prev, next);
            prev = next;
        }

        return Mathf.Max(len, 0.001f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2)
            return;

        int steps = 120;
        Vector3 prev = SamplePreviewPath(0f);

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 next = SamplePreviewPath(t);

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                Gizmos.DrawSphere(waypoints[i].position, 0.15f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(waypoints[i].position + Vector3.up * 0.25f, $"WP {i}");
#endif
            }
        }

        if (useTeleportSegment && IsTeleportSegmentValid())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                waypoints[teleportFromWaypoint].position,
                waypoints[teleportToWaypoint].position
            );
        }

        if (lookAtTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lookAtTarget.position, 0.3f);
        }
    }

    private Vector3 SamplePreviewPath(float t)
    {
        return SampleCustomPath(t);
    }
#endif
}
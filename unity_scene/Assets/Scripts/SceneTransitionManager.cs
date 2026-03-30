using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade")]
    [Tooltip("Couleur du fondu (noir par défaut).")]
    public Color fadeColor = Color.black;

    [Range(0.1f, 3f)]
    public float fadeDuration = 1f;
    
    private CanvasGroup _canvasGroup;
    public float delayBeforeFade = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildOverlay();
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(Transition(sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(Transition(sceneIndex.ToString(), useIndex: true));
    }
    
    public void FadeIn()
    {
        StartCoroutine(Fade(1f, 0f));
    }

    private IEnumerator Transition(string scene, bool useIndex = false)
    {
        yield return new WaitForSeconds(delayBeforeFade);
        yield return StartCoroutine(Fade(0f, 1f));
        
        AsyncOperation op = useIndex
            ? SceneManager.LoadSceneAsync(int.Parse(scene))
            : SceneManager.LoadSceneAsync(scene);

        yield return op;
        
        yield return StartCoroutine(Fade(1f, 0f));
    }

    private IEnumerator Fade(float from, float to)
    {
        _canvasGroup.alpha = from;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }

    private void BuildOverlay()
    {
        // Canvas
        var canvasGO = new GameObject("_FadeOverlay");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        
        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var img = imgGO.AddComponent<Image>();
        img.color = fadeColor;

        var rect = imgGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        _canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;
    }
}
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameHudController : MonoBehaviour
{
    public GameCycleDirector director;
    public DefendObjective objective;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI missionText;
    public TextMeshProUGUI objectiveText;
    public Image objectiveFill;
    public GameObject objectivePanel;
    public GameObject resultPanel;
    public TextMeshProUGUI resultTitle;
    public TextMeshProUGUI resultStats;
    public Button returnButton;

    private bool _resultShown;
    private bool _returningToStart;
    private GameObject _resultBackdrop;
    private Image _resultPanelImage;
    private Image _resultAccent;
    private Image _returnButtonImage;
    private Outline _resultOutline;
    private TextMeshProUGUI _resultHeader;
    private TextMeshProUGUI _resultStatus;
    private TextMeshProUGUI _resultHint;
    private float _lastObjectiveHealth = -1f;
    private float _objectiveDamageFlashUntil;

    void Start()
    {
        if (director == null) director = FindFirstObjectByType<GameCycleDirector>();
        if (objective == null) objective = FindFirstObjectByType<DefendObjective>();
        EnsureEventSystem();
        ConfigureResultPresentation();
        if (resultPanel != null) resultPanel.SetActive(false);
        if (_resultBackdrop != null) _resultBackdrop.SetActive(false);
        if (returnButton != null) returnButton.onClick.AddListener(ReturnToStart);
        if (objective != null)
        {
            objective.HealthChanged -= OnObjectiveHealthChanged;
            objective.HealthChanged += OnObjectiveHealthChanged;
            OnObjectiveHealthChanged(objective.CurrentHealth, objective.maxHealth);
        }
    }

    void Update()
    {
        if (director == null) return;
        UpdateWavePanel();
        UpdateObjectivePanel();
        if (!_resultShown && (director.Phase == GameCycleDirector.CyclePhase.Victory ||
                             director.Phase == GameCycleDirector.CyclePhase.Failed))
            ShowResult();
        if (_resultShown && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            ReturnToStart();
    }

    void UpdateWavePanel()
    {
        if (waveText != null)
            waveText.text = $"웨이브 {Mathf.Max(1, director.CycleNumber)} / {director.totalCycles}";

        switch (director.Phase)
        {
            case GameCycleDirector.CyclePhase.Preparing:
                if (phaseText != null) phaseText.text = $"전투 준비  {Mathf.CeilToInt(director.PhaseTimeRemaining)}";
                if (missionText != null) missionText.text = director.CycleNumber == 1
                    ? "시작 구역을 확보하고 첫 공세에 대비하세요"
                    : "탄약을 확보하고 다음 공세에 대비하세요";
                break;
            case GameCycleDirector.CyclePhase.Defending:
                if (phaseText != null) phaseText.text = director.waveSpawner.BossActive
                    ? "보스  -  암 오버로드"
                    : $"남은 감염자  {director.waveSpawner.RemainingCount}";
                if (missionText != null) missionText.text = director.CycleNumber == 1
                    ? "첫 번째 감염자 무리에서 살아남으세요"
                    : director.CycleNumber is 2 or 3
                        ? "발전기를 공격하는 감염자를 막으세요"
                        : "구조 헬기가 올 때까지 최후의 공세를 버티세요";
                break;
            case GameCycleDirector.CyclePhase.GeneratorActivation:
                if (phaseText != null) phaseText.text = "발전기 구역으로 이동";
                if (missionText != null) missionText.text = "표시된 발전기에 접근해 방어 시스템을 가동하세요";
                break;
            case GameCycleDirector.CyclePhase.Cleared:
                if (phaseText != null) phaseText.text = $"구역 확보  |  다음 단계 {Mathf.CeilToInt(director.PhaseTimeRemaining)}";
                if (missionText != null) missionText.text = "재정비하고 보급품을 회수하세요";
                break;
            case GameCycleDirector.CyclePhase.HelipadTransit:
                if (waveText != null) waveText.text = "최종 방어 준비";
                if (phaseText != null) phaseText.text = "헬기 착륙 지점으로 이동";
                if (missionText != null) missionText.text = "미니맵에 점멸하는 헬기 착륙 지점으로 이동하세요";
                break;
            case GameCycleDirector.CyclePhase.Extraction:
                if (waveText != null) waveText.text = "최종 탈출";
                if (phaseText != null) phaseText.text = "헬리콥터 접근 중";
                if (missionText != null) missionText.text = "임무 완료! 구조 헬기에 탑승합니다";
                break;
            case GameCycleDirector.CyclePhase.Victory:
                if (phaseText != null) phaseText.text = "방어 작전 완료";
                break;
            case GameCycleDirector.CyclePhase.Failed:
                if (phaseText != null) phaseText.text = "방어 실패";
                break;
        }
    }

    void UpdateObjectivePanel()
    {
        if (objective == null) return;
        if (objectivePanel != null)
            objectivePanel.SetActive(objective.IsOperational);
        if (!objective.IsOperational) return;

        OnObjectiveHealthChanged(objective.CurrentHealth, objective.maxHealth);
    }

    void OnObjectiveHealthChanged(float current, float maximum)
    {
        if (objective == null) return;
        if (_lastObjectiveHealth >= 0f && current < _lastObjectiveHealth)
            _objectiveDamageFlashUntil = Time.unscaledTime + 0.18f;
        _lastObjectiveHealth = current;
        if (objectivePanel != null) objectivePanel.SetActive(objective.IsOperational);
        float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (objectiveFill != null)
        {
            // A sprite-less UI Image does not visually honor Filled/fillAmount in every
            // Unity version. Shrink the fill rect itself so the bar always matches HP.
            objectiveFill.fillAmount = ratio;
            RectTransform fillRect = objectiveFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            objectiveFill.color = Time.unscaledTime < _objectiveDamageFlashUntil
                ? new Color(1f, 0.92f, 0.58f)
                : ratio <= 0.3f ? new Color(1f, 0.12f, 0.06f) : new Color(0.95f, 0.58f, 0.14f);
        }
        if (objectiveText != null)
            objectiveText.text = ratio <= 0.3f
                ? $"발전기 위험  {Mathf.CeilToInt(current)}"
                : $"발전기  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
    }

    void ShowResult()
    {
        _resultShown = true;
        bool victory = director.Phase == GameCycleDirector.CyclePhase.Victory;
        LockPlayerControls();
        ConfigureResultPresentation();
        Color accent = victory ? new Color(0.18f, 0.88f, 0.62f, 1f) : new Color(1f, 0.34f, 0.10f, 1f);
        if (_resultBackdrop != null) _resultBackdrop.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(true);
        if (_resultAccent != null) _resultAccent.color = accent;
        if (_resultOutline != null) _resultOutline.effectColor = Color.clear;
        if (_returnButtonImage != null) _returnButtonImage.color = new Color(accent.r * 0.68f, accent.g * 0.68f, accent.b * 0.68f, 0.96f);
        if (_resultStatus != null)
        {
            _resultStatus.text = victory ? "MISSION COMPLETE" : "MISSION FAILED";
            _resultStatus.color = accent;
        }
        if (resultTitle != null)
        {
            resultTitle.text = victory ? "지역 방어 완료" : "작전 실패";
            resultTitle.color = Color.white;
        }
        if (resultStats != null)
        {
            string reason = victory ? "발전기 방어 성공" : LocalizeReason(GameSessionState.FailureReason);
            int minutes = Mathf.FloorToInt(GameSessionState.TotalPlayTime / 60f);
            int seconds = Mathf.FloorToInt(GameSessionState.TotalPlayTime % 60f);
            resultStats.text = $"<color=#8994A3>처치한 감염체</color>  <b>{GameSessionState.ZombieKills}</b>" +
                               $"\n<color=#8994A3>도달 웨이브</color>    <b>{GameSessionState.ReachedWave} / {director.totalCycles}</b>" +
                               $"\n<color=#8994A3>작전 시간</color>      <b>{minutes:00}:{seconds:00}</b>" +
                               $"\n<color=#8994A3>종료 사유</color>      <b>{reason}</b>";
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LockPlayerControls()
    {
        PlayerController movement = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        WeaponManager weapons = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        MouseLook look = FindFirstObjectByType<MouseLook>(FindObjectsInactive.Include);
        if (movement != null) movement.enabled = false;
        if (weapons != null) weapons.enabled = false;
        if (look != null) look.enabled = false;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    [ContextMenu("Configure Result Presentation")]
    public void ConfigureResultPresentation()
    {
        if (resultPanel == null) return;
        RectTransform panelRect = resultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(780f, 520f);

        _resultPanelImage = resultPanel.GetComponent<Image>();
        if (_resultPanelImage == null) _resultPanelImage = resultPanel.AddComponent<Image>();
        _resultPanelImage.color = new Color(0.035f, 0.045f, 0.062f, 0.985f);
        _resultPanelImage.raycastTarget = true;
        _resultOutline = resultPanel.GetComponent<Outline>();
        if (_resultOutline == null) _resultOutline = resultPanel.AddComponent<Outline>();
        _resultOutline.effectDistance = new Vector2(1f, -1f);
        _resultOutline.effectColor = Color.clear;

        Transform parent = resultPanel.transform.parent;
        Transform backdropTransform = parent.Find("ResultBackdrop");
        if (backdropTransform == null)
        {
            GameObject backdrop = new("ResultBackdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            backdropTransform = backdrop.transform;
        }
        _resultBackdrop = backdropTransform.gameObject;
        RectTransform backdropRect = backdropTransform.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdropTransform.GetComponent<Image>();
        backdropImage.color = new Color(0.008f, 0.012f, 0.02f, 0.82f);
        backdropImage.raycastTarget = true;
        backdropTransform.SetSiblingIndex(resultPanel.transform.GetSiblingIndex());
        resultPanel.transform.SetAsLastSibling();
        _resultBackdrop.SetActive(resultPanel.activeSelf);

        _resultAccent = GetOrCreateResultImage("TopAccent", resultPanel.transform, new Color(1f, 0.42f, 0.12f, 1f));
        SetResultRect(_resultAccent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 6f));

        _resultHeader = GetOrCreateResultText("ResultHeader", resultPanel.transform, "LAST SHIFT  //  작전 결과 보고", 17f, FontStyles.Bold);
        SetResultRect(_resultHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(680f, 32f));
        _resultHeader.color = new Color(0.56f, 0.62f, 0.70f, 1f);
        _resultHeader.characterSpacing = 2f;

        _resultStatus = GetOrCreateResultText("ResultStatus", resultPanel.transform, "MISSION FAILED", 20f, FontStyles.Bold);
        SetResultRect(_resultStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(680f, 34f));
        _resultStatus.characterSpacing = 5f;

        if (resultTitle != null)
        {
            resultTitle.fontSize = 48f;
            resultTitle.fontStyle = FontStyles.Bold;
            resultTitle.alignment = TextAlignmentOptions.Center;
            SetResultRect(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(680f, 68f));
        }

        Image statsPlate = GetOrCreateResultImage("ResultStatsPlate", resultPanel.transform, new Color(0.06f, 0.075f, 0.10f, 0.96f));
        SetResultRect(statsPlate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(650f, 210f));
        statsPlate.transform.SetSiblingIndex(Mathf.Max(1, resultStats != null ? resultStats.transform.GetSiblingIndex() : 1));
        if (resultStats != null)
        {
            resultStats.fontSize = 25f;
            resultStats.fontStyle = FontStyles.Normal;
            resultStats.alignment = TextAlignmentOptions.MidlineLeft;
            resultStats.lineSpacing = 14f;
            resultStats.color = new Color(0.93f, 0.95f, 0.98f, 1f);
            SetResultRect(resultStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(560f, 190f));
            resultStats.transform.SetAsLastSibling();
        }

        if (returnButton != null)
        {
            RectTransform buttonRect = returnButton.GetComponent<RectTransform>();
            SetResultRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(310f, 66f));
            _returnButtonImage = returnButton.GetComponent<Image>();
            if (_returnButtonImage == null) _returnButtonImage = returnButton.gameObject.AddComponent<Image>();
            _returnButtonImage.color = new Color(0.68f, 0.23f, 0.07f, 0.96f);
            ColorBlock colors = returnButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            returnButton.colors = colors;
            TextMeshProUGUI label = returnButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) { label.text = "작전실로 복귀"; label.fontSize = 24f; label.fontStyle = FontStyles.Bold; }
            returnButton.transform.SetAsLastSibling();
        }

        _resultHint = GetOrCreateResultText("ResultHint", resultPanel.transform, "ENTER 키 또는 버튼을 눌러 계속", 14f, FontStyles.Normal);
        SetResultRect(_resultHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(500f, 18f));
        _resultHint.color = new Color(0.48f, 0.54f, 0.62f, 1f);
        _resultHint.transform.SetAsLastSibling();
    }

    Image GetOrCreateResultImage(string objectName, Transform parent, Color color)
    {
        Transform found = parent.Find(objectName);
        Image image;
        if (found != null) image = found.GetComponent<Image>();
        else
        {
            GameObject item = new(objectName, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            image = item.GetComponent<Image>();
        }
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TextMeshProUGUI GetOrCreateResultText(string objectName, Transform parent, string content, float size, FontStyles style)
    {
        Transform found = parent.Find(objectName);
        TextMeshProUGUI text;
        if (found != null) text = found.GetComponent<TextMeshProUGUI>();
        else
        {
            GameObject item = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            text = item.GetComponent<TextMeshProUGUI>();
        }
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (resultTitle != null && resultTitle.font != null) text.font = resultTitle.font;
        return text;
    }

    static void SetResultRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static string LocalizeReason(string reason) => reason switch
    {
        "PLAYER LOST" => "플레이어 사망",
        "GENERATOR DESTROYED" => "발전기 파괴",
        _ => string.IsNullOrWhiteSpace(reason) ? "작전 실패" : reason
    };

    void ReturnToStart()
    {
        if (_returningToStart) return;
        _returningToStart = true;
        if (returnButton != null) returnButton.interactable = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScene", LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (objective != null) objective.HealthChanged -= OnObjectiveHealthChanged;
        if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToStart);
    }
}

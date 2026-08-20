using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>무기 슬롯 전환 시 하단에 표시 후 Fade Out</summary>
public class WeaponSlotUI : MonoBehaviour
{
    [Header("Slot Icons (1 Rifle / 2 Sniper / 3 Pistol)")]
    public Image[] slotIcons;

    public CanvasGroup canvasGroup;

    [Header("Timing")]
    public float showDuration = 2f;
    public float fadeDuration = 1f;

    private CancellationTokenSource _fadeCts;
    private static readonly Color[] SlotColors =
    {
        new(0.90f, 0.16f, 0.13f),
        new(0.16f, 0.43f, 0.95f),
        new(0.12f, 0.78f, 0.32f)
    };

    public void ConfigureAvailableSlots(WeaponBase[] weapons)
    {
        if (transform is RectTransform panelRect)
            panelRect.sizeDelta = new Vector2(304f, 68f);
        HorizontalLayoutGroup layoutGroup = GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;
            bool available = weapons != null && i < weapons.Length && weapons[i] != null;
            slotIcons[i].gameObject.SetActive(available);
            if (!available) continue;

            LayoutElement layout = slotIcons[i].GetComponent<LayoutElement>();
            if (layout != null) layout.enabled = false;
            RectTransform slotRect = slotIcons[i].rectTransform;
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(88f, 58f);
            slotRect.anchoredPosition = new Vector2((i - 1) * 96f, 0f);
            slotIcons[i].name = i switch { 0 => "Slot1_Rifle", 1 => "Slot2_Sniper", _ => "Slot3_Pistol" };
            EnsureWeaponGraphic(slotIcons[i], i);
            EnsureSlotNumber(slotIcons[i], i + 1);
        }
        Canvas.ForceUpdateCanvases();
    }

    public void ShowSlotChange(int activeIndex)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;
            Color slotColor = SlotColors[Mathf.Clamp(i, 0, SlotColors.Length - 1)];
            slotColor.a = i == activeIndex ? 0.56f : 0.22f;
            slotIcons[i].color = slotColor;
            HUDIconGraphic weaponGraphic = slotIcons[i].GetComponentInChildren<HUDIconGraphic>(true);
            if (weaponGraphic != null)
            {
                weaponGraphic.color = i == activeIndex
                    ? new Color(1f, 1f, 1f, 0.96f)
                    : new Color(0.88f, 0.92f, 0.96f, 0.68f);
                weaponGraphic.SetVerticesDirty();
            }
            TextMeshProUGUI number = slotIcons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (number != null) number.color = new Color(1f, 1f, 1f, i == activeIndex ? 0.98f : 0.58f);
        }

        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        FadeOutAsync(_fadeCts.Token).Forget();
    }

    static void EnsureWeaponGraphic(Image slot, int index)
    {
        HUDIconGraphic[] graphics = slot.GetComponentsInChildren<HUDIconGraphic>(true);
        Transform existing = graphics.Length > 0 ? graphics[0].transform : null;
        for (int i = 1; i < graphics.Length; i++)
            if (graphics[i] != null)
            {
                if (Application.isPlaying) Destroy(graphics[i].gameObject);
                else DestroyImmediate(graphics[i].gameObject);
            }
        HUDIconGraphic graphic;
        if (existing != null)
        {
            graphic = existing.GetComponent<HUDIconGraphic>();
        }
        else
        {
            GameObject iconObject = new("WeaponGraphic", typeof(RectTransform), typeof(HUDIconGraphic));
            iconObject.transform.SetParent(slot.transform, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 8f);
            rect.offsetMax = new Vector2(-7f, -8f);
            graphic = iconObject.GetComponent<HUDIconGraphic>();
            graphic.raycastTarget = false;
        }

        graphic.iconType = index switch
        {
            0 => HUDIconGraphic.IconType.Rifle,
            1 => HUDIconGraphic.IconType.Sniper,
            _ => HUDIconGraphic.IconType.Pistol
        };
        graphic.SetVerticesDirty();
    }

    static void EnsureSlotNumber(Image slot, int number)
    {
        Transform existing = slot.transform.Find("SlotNumber");
        TextMeshProUGUI label;
        if (existing != null) label = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            GameObject labelObject = new("SlotNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(slot.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(5f, -4f);
            rect.sizeDelta = new Vector2(18f, 20f);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
        label.text = number.ToString();
    }

    async UniTaskVoid FadeOutAsync(CancellationToken token)
    {
        bool cancelled = await UniTask.Delay((int)(showDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return;
        }
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
    }
}

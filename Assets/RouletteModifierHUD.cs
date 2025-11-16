using UnityEngine;
using TMPro;

public class RouletteModifierHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI modifierLabel;

    private float _timer;

    private void OnEnable()
    {
        RouletteModifierManager.OnShowModifierLabel += HandleShowLabel;
    }

    private void OnDisable()
    {
        RouletteModifierManager.OnShowModifierLabel -= HandleShowLabel;
    }

    private void HandleShowLabel(string text, float time)
    {
        if (modifierLabel == null) return;

        modifierLabel.text = text;
        modifierLabel.gameObject.SetActive(true);
        _timer = time;
    }

    private void Update()
    {
        if (modifierLabel == null) return;
        if (_timer <= 0f) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            modifierLabel.gameObject.SetActive(false);
        }
    }
}

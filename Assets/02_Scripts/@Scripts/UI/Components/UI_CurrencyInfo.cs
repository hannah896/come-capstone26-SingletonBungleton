using Blossom.Preference;
using UnityEngine;

public class UI_CurrencyInfo : UI_Button
{
    #region Fields

    private CurrencyPrefs _prefs;
    private UI_Text _txtValue;

    #endregion

    #region MonoBehaviours

    protected void OnDisable()
    {
        if (_prefs != null) _prefs.Currency.OnDisplayValueChanged -= OnValueChanged;
    }

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _txtValue = this.gameObject.FindChild<UI_Text>("txtValue");
        this.SetEvent(OnButton);

        return true;
    }

    public void Set()
    {
        Initialize();

        _prefs = Prefs.Get<CurrencyPrefs>();
        _prefs.Currency.OnDisplayValueChanged -= OnValueChanged;
        _prefs.Currency.OnDisplayValueChanged += OnValueChanged;
        _txtValue.Text = _prefs.Currency.DisplayValue.GetFormattedCurrency();
    }

    #endregion

    #region Events

    private void OnValueChanged(int value)
    {
        _txtValue.Text = _prefs.Currency.DisplayValue.GetFormattedCurrency();
    }

    private void OnButton()
    {
        // Main.UI.OpenPanel<UI_Panel_Store>().Set();
    }

    #endregion
}
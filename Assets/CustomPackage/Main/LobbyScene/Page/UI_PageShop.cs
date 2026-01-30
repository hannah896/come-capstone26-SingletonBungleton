using System.Collections;
using System.Linq;
using Blossom.Preference;
using UnityEngine;

public class UI_PageShop : UI_Page
{
    #region Fields

    private GameObject _specialOfferRoot;
    private CurrencyPrefs _currencyPrefs;

    #endregion

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _currencyPrefs = Prefs.Get<CurrencyPrefs>();
        gameObject.FindChild<UI_Button>("Btn_BuyCoinAds").SetEvent(OnClickAdsCoin);

        return true;
    }

    protected override PageType GetPageType() => PageType.Shop;

    private void OnClickAdsCoin()
    {
        //Main.Ads.ShowReward
        //(
        //    () => _currencyPrefs.Currency.Value += 250,
        //    null
        //);
    }
}
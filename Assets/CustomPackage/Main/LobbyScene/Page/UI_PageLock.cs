
using System.Collections;
using System.Linq;
using UnityEngine;

public class UI_PageLock : UI_Page
{
    #region Fields
    
    private GameObject _specialOfferRoot;

    #endregion
    
    public override bool Initialize()
    {
        if(!base.Initialize()) return false;
        
        return true;
    }

    protected override PageType GetPageType() => PageType.Lock;
}

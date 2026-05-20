//using UnityEngine;

//public class UIKeyHandler : MonoBehaviour
//{
//    [SerializeField] private KeyCode inventoryKey = KeyCode.Tab;
//    [SerializeField] private KeyCode craftingKey = KeyCode.C;

//    [SerializeField] private GameObject inventoryPanel;
//    [SerializeField] private GameObject craftingPanel;

//    private CanvasGroup _craftingCG;
//    private bool _craftingOpen = false;
//    private bool _inventoryOpen = false;

//    private void Start()
//    {
//        _craftingCG = craftingPanel.GetComponent<CanvasGroup>();
//        inventoryPanel.SetActive(false);
//        SetCrafting(false);
//    }

//    private void Update()
//    {
//        if (Input.GetKeyDown(inventoryKey)) ToggleInventory();
//        if (Input.GetKeyDown(craftingKey)) ToggleCrafting();
//    }

//    private void ToggleInventory()
//    {
//        _inventoryOpen = !_inventoryOpen;
//        inventoryPanel.SetActive(_inventoryOpen);
//    }

//    private void ToggleCrafting()
//    {
//        _craftingOpen = !_craftingOpen;
//        SetCrafting(_craftingOpen);
//        if (_craftingOpen)
//            craftingPanel.GetComponent<CraftingUI>().OpenDefault();
//    }

//    private void SetCrafting(bool open)
//    {
//        _craftingCG.alpha = open ? 1 : 0;
//        _craftingCG.blocksRaycasts = open;
//    }
//}
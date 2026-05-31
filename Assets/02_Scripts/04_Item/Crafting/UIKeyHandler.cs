using UnityEngine;
using UnityEngine.InputSystem;

public class UIKeyHandler : MonoBehaviour
{
    [SerializeField] private Key inventoryKey = Key.Tab;
    [SerializeField] private Key craftingKey  = Key.C;

    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private CraftingUI craftingUI;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[craftingKey].wasPressedThisFrame)
            ToggleCrafting();

        if (keyboard[inventoryKey].wasPressedThisFrame)
            ToggleInventory();
    }

    private void ToggleInventory()
    {
        if (inventoryPanel == null) return;
        inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    }

    private void ToggleCrafting()
    {
        if (craftingUI == null) return;
        craftingUI.Toggle();
    }
}

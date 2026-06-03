using UnityEngine;
using UnityEngine.InputSystem;

public class UIKeyHandler : MonoBehaviour
{
    [SerializeField] private Key craftingKey = Key.C;
    [SerializeField] private CraftingUI craftingUI;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[craftingKey].wasPressedThisFrame)
            ToggleCrafting();
    }

    private void ToggleCrafting()
    {
        if (craftingUI == null)
            craftingUI = Object.FindObjectOfType<CraftingUI>(true);
        if (craftingUI == null) return;
        craftingUI.Toggle();
    }
}

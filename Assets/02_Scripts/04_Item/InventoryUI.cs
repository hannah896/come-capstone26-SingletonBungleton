using System.Collections.Generic;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotContainer;

    [Header("열기 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    private bool _isOpen = false;

    private void Start()
    {
        BuildUI();
        InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    private void Toggle()
    {
        _isOpen = !_isOpen;
        gameObject.SetActive(_isOpen);
        if (_isOpen) RefreshUI();
    }

    private void BuildUI()
    {
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);
        _slotUIs.Clear();

        for (int i = 0; i < InventoryManager.Instance.slots.Count; i++)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slotUIs.Add(go.GetComponent<InventorySlotUI>());
        }
        RefreshUI();
    }

    private void RefreshUI()
    {
        var slots = InventoryManager.Instance.slots;
        var stacks = InventoryManager.Instance.stackCounts;
        for (int i = 0; i < _slotUIs.Count; i++)
            _slotUIs[i].Refresh(i, slots[i], stacks[i]);
    }
}
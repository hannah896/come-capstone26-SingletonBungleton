using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blossom.Preference;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ItemCollector {

    

    public static void CollectItemNANA(CollectorItemType type, int amount, Vector3 startPosition) {
        CurrencyPrefs prefs = Prefs.Get<CurrencyPrefs>();
        CollectorMoveDirection direction =
            type == CollectorItemType.Currency || type == CollectorItemType.Lives ||
            type == CollectorItemType.UnlimitedLives
                ? CollectorMoveDirection.Top
                : CollectorMoveDirection.Bottom;

        switch (type) {
            case CollectorItemType.Currency:
                prefs.Currency.Value += amount;
                break;
            //case CollectorItemType.UnlimitedLives:
            //    Main.Lives.AddUnlimitedTime(amount);
            //    break;
            //case CollectorItemType.MaxLives:
            //    Main.Lives.AddBonusLives();
            //    break;
            // case CollectorItemType.Clock:
            //     prefs.Clock.Value += amount;
            //     break;
            // case CollectorItemType.Wand:
            //     prefs.Wand.Value += amount;
            //     break;
            // case CollectorItemType.UFO:
            //     prefs.UFO.Value += amount;
            //     break;
        }
        ItemCollector.Collect(type, startPosition, amount, direction);
    }
    
    public static void Collect(CollectorItemType type, Vector3 startPosition, int amount,
        CollectorMoveDirection direction = CollectorMoveDirection.Top, Action cbOnCompleted = null) {
        CollectItem(type, startPosition, amount, direction, cbOnCompleted);
    }
    
    private static void CollectItem(CollectorItemType type, Vector3 startPosition, int amount,
        CollectorMoveDirection direction, Action cbOnCompleted = null) {
        if (!IsValidToPlay(type)) {
            cbOnCompleted?.Invoke();
            return;
        }

        CollectorItemGroup group = new(type, amount);
        CollectItem(type, group, startPosition, direction, cbOnCompleted);
    }
    
    private static bool IsValidToPlay(CollectorItemType type) {
        return type == CollectorItemType.None ? false : ItemReceiver.GetTransform(type);
    }

    private static void CollectItem(CollectorItemType type, CollectorItemGroup group, Vector3 startPosition,
        CollectorMoveDirection direction, Action cbOnCompleted = null) {
        Main.StartCoroutine(CoCollectItem(type, group, startPosition, direction, cbOnCompleted));
    }

    private static IEnumerator CoCollectItem(CollectorItemType type, CollectorItemGroup group, Vector3 startPosition,
        CollectorMoveDirection direction, Action cbOnCompleted = null) {
        List<int> amounts = group.GetGroupAmounts();

        List<bool> flags = new();

        for (int i = 0; i < amounts.Count; i++) {
            int amount = amounts[i];

            CollectorItem item = new(type, amount);
            //ItemDisplayObject displayObject = Main.Object.Instantiate<ItemDisplayObject>();
            ItemDisplayUI displayObject = new GameObject("BoardObject").AddComponent<ItemDisplayUI>();
            displayObject.Set(item, startPosition);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(0f, 0.4f);
            Vector3 offset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            float curveHeight = Random.Range(1f, 2f);
            //float duration = 0.5f;
            float duration = 0.5f;
            ItemDisplayController controller = new(displayObject);

            flags.Add(false);
            int index = i;
            controller.MoveTo(startPosition, offset, duration, curveHeight, direction, () => {
                flags[index] = true;
                
                // TODO::
                //Haptic.Weak();
                //if (type == CollectorItemType.Currency) Main.Audio.PlaySFX(SFX.Coin);
            });

            //float delay = Mathf.Max(0, 0.2f + Random.Range(-0.1f, 0.1f));
            float delay = Mathf.Max(0, 0.075f + Random.Range(-0.05f, 0.05f));
            yield return new WaitForSeconds(delay);
        }

        yield return new WaitUntil(() => flags.All(f => f));
        cbOnCompleted?.Invoke();
    }

}

public class CollectorItemGroup {

    #region Properties

    public CollectorItemType Type { get; private set; }
    public int TotalAmount { get; private set; }

    #endregion

    #region Constructor

    public CollectorItemGroup(CollectorItemType type, int totalAmount) {
        Type = type;
        TotalAmount = totalAmount;
    }

    #endregion

    public List<int> GetGroupAmounts() {
        List<int> amounts = new();
        switch (Type) {
            case CollectorItemType.Currency: CalculateCurrencies(amounts); break;
            case CollectorItemType.UnlimitedLives: amounts.Add(TotalAmount); break;
            default: CalculateItems(amounts); break;
        }
        return amounts;
    }

    #region Calculate

    private void CalculateCurrencies(List<int> amounts) {
        int remaining = TotalAmount;
        if (remaining >= 10000) {
            int count = remaining / 10000;
            for (int i = 0; i < count; i++) amounts.Add(10000);
            remaining %= 10000;
        }

        if (remaining >= 1000) {
            int count = remaining / 1000;
            for (int i = 0; i < count; i++) amounts.Add(1000);
            remaining %= 1000;
        }

        if (remaining >= 100) {
            int count = remaining / 100;
            for (int i = 0; i < count; i++) amounts.Add(100);
            remaining %= 100;
        }

        while (remaining > 0) {
            // int amount = Math.Min(remaining, 10);
            int amount = Math.Min(remaining, 5);
            amounts.Add(amount);
            remaining -= amount;
        }
    }

    private void CalculateItems(List<int> amounts) {
        int remaining = TotalAmount;
        while (remaining >= 5) {
            amounts.Add(5);
            remaining -= 5;
        }

        for (int i = 0; i < remaining; i++) amounts.Add(1);
    }

    #endregion
    
}

public class CollectorItem {
    public CollectorItemType Type { get; private set; }
    public int Amount { get; private set; }

    public CollectorItem(CollectorItemType type, int amount) {
        Type = type;
        Amount = amount;
    }
}

public enum CollectorItemType {
    None = -1,
    Currency = 0,
    Lives,
    UnlimitedLives,
    MaxLives,
    Clock,
    Wand,
    UFO,
    LuckyPass,
    StarterPack,
    TigerPack,
    MetaCurrency,
}

public enum CollectorMoveDirection {
    Top,
    Bottom,
    Left,
    Right,
}
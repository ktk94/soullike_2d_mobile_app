using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCraft.Farming
{
    /// <summary>
    /// 인벤토리 슬롯 하나를 표현한다.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int quantity;

        public InventorySlot(ItemData item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }

        public bool IsEmpty => item == null || quantity <= 0;
    }

    /// <summary>
    /// 플레이어 인벤토리. 싱글턴 MonoBehaviour.
    /// 아이템 추가/제거/조회와 스택 관리를 담당한다.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxSlots = 40;

        public int MaxSlots => maxSlots;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        /// <summary>
        /// 인벤토리 내용이 변경될 때 발생한다.
        /// </summary>
        public event Action OnInventoryChanged;

        private readonly List<InventorySlot> _slots = new();

        // ── Lifecycle ────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────

        /// <summary>
        /// 아이템을 인벤토리에 추가한다.
        /// 스택 가능 아이템은 기존 슬롯에 합산하고, 넘치면 새 슬롯을 생성한다.
        /// </summary>
        /// <returns>실제로 추가된 수량. 인벤토리가 가득 차면 0 이상의 부분 추가도 가능.</returns>
        public int AddItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return 0;

            int remaining = quantity;

            // 스택 가능 아이템이면 기존 슬롯부터 채운다
            if (item.stackable)
            {
                for (int i = 0; i < _slots.Count && remaining > 0; i++)
                {
                    if (_slots[i].item == item && _slots[i].quantity < item.maxStack)
                    {
                        int space = item.maxStack - _slots[i].quantity;
                        int toAdd = Mathf.Min(remaining, space);
                        _slots[i].quantity += toAdd;
                        remaining -= toAdd;
                    }
                }
            }

            // 남은 수량을 새 슬롯에 배치
            while (remaining > 0 && _slots.Count < maxSlots)
            {
                int perSlot = item.stackable ? Mathf.Min(remaining, item.maxStack) : 1;
                _slots.Add(new InventorySlot(item, perSlot));
                remaining -= perSlot;
            }

            int added = quantity - remaining;
            if (added > 0)
            {
                OnInventoryChanged?.Invoke();
            }

            return added;
        }

        /// <summary>
        /// 인벤토리에서 아이템을 제거한다.
        /// </summary>
        /// <returns>실제로 제거된 수량.</returns>
        public int RemoveItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return 0;

            int remaining = quantity;

            // 뒤에서부터 순회하여 제거 (삭제 시 인덱스 문제 방지)
            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_slots[i].item != item) continue;

                int toRemove = Mathf.Min(remaining, _slots[i].quantity);
                _slots[i].quantity -= toRemove;
                remaining -= toRemove;

                if (_slots[i].quantity <= 0)
                {
                    _slots.RemoveAt(i);
                }
            }

            int removed = quantity - remaining;
            if (removed > 0)
            {
                OnInventoryChanged?.Invoke();
            }

            return removed;
        }

        /// <summary>
        /// 특정 아이템이 지정 수량 이상 있는지 확인한다.
        /// </summary>
        public bool HasItem(ItemData item, int quantity = 1)
        {
            return GetItemCount(item) >= quantity;
        }

        /// <summary>
        /// 특정 아이템의 총 보유 수량을 반환한다.
        /// </summary>
        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;

            int count = 0;
            foreach (var slot in _slots)
            {
                if (slot.item == item)
                    count += slot.quantity;
            }
            return count;
        }

        /// <summary>
        /// 빈 슬롯 수를 반환한다.
        /// </summary>
        public int GetEmptySlotCount()
        {
            return maxSlots - _slots.Count;
        }

        /// <summary>
        /// 인벤토리가 가득 찼는지 확인한다.
        /// </summary>
        public bool IsFull()
        {
            return _slots.Count >= maxSlots;
        }

        /// <summary>
        /// 인벤토리를 비운다.
        /// </summary>
        public void Clear()
        {
            _slots.Clear();
            OnInventoryChanged?.Invoke();
        }
    }
}

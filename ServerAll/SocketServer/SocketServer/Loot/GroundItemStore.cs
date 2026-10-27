namespace Server.Loot;

// 자기 락을 자기가 잡는다. Room 을 모른다 — 줍기 시전자 위치는 인자로 받는다. 브로드캐스트는 호출자 책임.
public sealed class GroundItemStore
{
    public const float PickupRange = 3f;

    private readonly Dictionary<int, GroundItem> _items = new();
    private int _nextGroundId;

    public GroundItem Spawn(int itemId, int qty, float x, float y, float z)
    {
        lock (_items)
        {
            int id = ++_nextGroundId;
            var item = new GroundItem
            {
                GroundId = id,
                ItemId = itemId,
                Qty = qty,
                PosX = x, PosY = y, PosZ = z,
            };
            _items[id] = item;
            return item;
        }
    }

    public IReadOnlyList<GroundItem> All()
    {
        lock (_items)
        {
            return _items.Values.ToList();
        }
    }

    // null = 이미 주워짐(경쟁 패배)·범위 밖·미존재. 락 안 Remove 가 1회만 성공 → 중복 지급 없음.
    // picker 위치는 호출자가 스냅샷으로 넘긴다(락 중첩 회피).
    public GroundItem? TryPickup(float pickerX, float pickerZ, int groundId)
    {
        lock (_items)
        {
            if (!_items.TryGetValue(groundId, out var item))
                return null;

            float dx = item.PosX - pickerX;
            float dz = item.PosZ - pickerZ;
            if (dx * dx + dz * dz > PickupRange * PickupRange)
                return null;

            _items.Remove(groundId);
            return item;
        }
    }

    public int Count
    {
        get { lock (_items) return _items.Count; }
    }
}

namespace Server.Loot;

/// <summary>
/// 방 하나의 <b>바닥 아이템 저장소</b>(서버 권위). 스폰·조회·줍기 경쟁 중재를 소유하고,
/// 자기 락을 자기가 잡는다.
///
/// <para><b>Room 을 모른다</b> — 줍기 거리 검증에 필요한 시전자 위치를 <b>인자로 받는다</b>.
/// 그래서 방·세션·액터 없이 단위 테스트로 경쟁 중재를 직접 칠 수 있다.
/// (예전엔 이 셋이 Room 안에 있어서 줍기 한 줄을 검증하려면 방과 플레이어를 다 세워야 했다.)</para>
///
/// <para>브로드캐스트(S_SpawnGroundItem / S_GroundItemRemoved)는 호출자 책임이다 —
/// 저장소는 상태만 바꾸고 누구에게 알릴지는 모른다.</para>
/// </summary>
public sealed class GroundItemStore
{
    /// <summary>줍기 가능 반경(평면 거리). 너무 먼 위치에서의 줍기 요청을 거른다.</summary>
    public const float PickupRange = 3f;

    private readonly Dictionary<int, GroundItem> _items = new();
    private int _nextGroundId;

    /// <summary>드랍 roll 결과 1건을 바닥에 놓는다. GroundId 는 방 단위 순차 발급(1부터).</summary>
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

    /// <summary>현재 바닥 아이템 전체(늦은 입장자에게 로스터를 재전송할 때).</summary>
    public IReadOnlyList<GroundItem> All()
    {
        lock (_items)
        {
            return _items.Values.ToList();
        }
    }

    /// <summary>
    /// 줍기 시도(경쟁 중재) — 거리 검증 후 <b>제거에 성공한 1명만</b> 아이템을 가져간다.
    /// 반환 non-null = 줍기 확정. null = 이미 주워짐(경쟁 패배)·범위 밖·미존재.
    /// 동시 픽업해도 락 안 Remove 가 1회만 성공하므로 중복 지급이 없다.
    /// </summary>
    /// <param name="pickerX">시전자 평면 X(호출자가 스냅샷을 떠서 넘긴다 — 락 중첩 회피).</param>
    /// <param name="pickerZ">시전자 평면 Z.</param>
    public GroundItem? TryPickup(float pickerX, float pickerZ, int groundId)
    {
        lock (_items)
        {
            if (!_items.TryGetValue(groundId, out var item))
                return null; // 이미 주워짐(경쟁 패배) 또는 존재하지 않음

            float dx = item.PosX - pickerX;
            float dz = item.PosZ - pickerZ;
            if (dx * dx + dz * dz > PickupRange * PickupRange)
                return null; // 줍기 범위 밖

            _items.Remove(groundId); // 경쟁 중재: 제거 성공 = 이 호출자가 승자
            return item;
        }
    }

    /// <summary>현재 바닥에 남은 개수(테스트·디버그).</summary>
    public int Count
    {
        get { lock (_items) return _items.Count; }
    }
}

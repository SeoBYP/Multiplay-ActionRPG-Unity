namespace Shared.Gameplay.Equipment
{
    /// <summary>
    /// 장비 착용 슬롯(타입). 클라(GUI)·서버 도메인 공통 — Shared.Gameplay 단일 소스.
    /// proto enum(EquipmentType)과 정수값 1:1 대응(경계에서 캐스팅 매핑). None=0 은 미지정 가드(proto default).
    ///
    /// 카탈로그가 채우는 슬롯은 Weapon/Armor 뿐이고 나머지는 GUI 표시·확장용 빈 슬롯이다.
    /// </summary>
    public enum EquipmentType
    {
        None = 0,
        Header = 1,
        Armor = 2,
        Shoose = 3,
        Glove = 4,
        Shield = 5,
        Weapon = 6,
        Ring = 7,
        Necklace = 8,
    }
}

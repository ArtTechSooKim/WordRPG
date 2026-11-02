using UnityEngine;
using WordRPG.Monsters;

namespace WordRPG.Items
{
    public enum ItemKind
    {
        Material,   // 성유물 강화 재료 (전투·보물상자·보스에서 얻음. 상점에서는 팔지 않음)
        Consumable, // 상처약 등 소모품 — healAmount만큼 회복, 필드·전투에서 사용
        Keepsake,   // 지역 도감 완성 징표 (기념물). 모으는 용도
        SkillDocument, // 기술문서 — 읽으면 기술을 배운다 (보스·보물상자에서 얻음). 배워도 사라지지 않아 언제든 다시 배울 수 있다
        Key           // 보스 방 열쇠 (#76) — 맵의 열쇠 받침대 'K'에서 얻음. 쓰지 않고 가지고만 있으면 보스 방 문을 열 수 있다
    }

    // 소모품의 특별한 효과 (#146): 필드에서 쓰면 PotionEffects에 쌓인다. 에셋에 번호로 저장 — 새 효과는 맨 뒤에
    public enum ItemEffect
    {
        None,
        NewWords, // 호기심 물약: 전투 문제에 새 단어만 effectAmount개
        Quiet     // 살금살금 물약: effectAmount걸음 동안 몬스터를 안 만남
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "WordRPG/Item", order = 20)]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName; // 예: "빛나는 잉크"
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private ItemKind kind;
        [SerializeField] private Sprite icon;
        [Tooltip("아트가 없을 때 쓰는 임시 색")]
        [SerializeField] private Color placeholderColor = Color.white;
        [Tooltip("소모품: 쓰면 회복하는 HP")]
        [SerializeField] private int healAmount;
        [Tooltip("기술문서: 배우는 기술")]
        [SerializeField] private SkillData taughtSkill;
        [Tooltip("소모품: 필드에서 쓰면 남는 효과 (#146)")]
        [SerializeField] private ItemEffect effect;
        [SerializeField] private int effectAmount;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemKind Kind => kind;
        public Sprite Icon => icon;
        public Color PlaceholderColor => placeholderColor;
        public int HealAmount => healAmount;
        public SkillData TaughtSkill => taughtSkill;
        public ItemEffect Effect => effect;
        public int EffectAmount => effectAmount;

        // 필드·전투에서 쓸 수 있는 회복 아이템
        // 가방에 놓이는 순서 (#147): 바로 쓰는 것부터 — 상처약 → 효과 물약 → 기술문서 → 재료 → 열쇠
        public int BagOrder => IsHealingItem ? 0 : IsEffectItem ? 1 : IsSkillDocument ? 2 : Kind == ItemKind.Key ? 4 : 3;

        public bool IsHealingItem => kind == ItemKind.Consumable && healAmount > 0;

        // 필드에서 쓰면 효과가 남는 물약 (#146)
        public bool IsEffectItem => kind == ItemKind.Consumable && effect != ItemEffect.None && effectAmount > 0;

        // 기술을 배울 수 있는 기술문서
        public bool IsSkillDocument => kind == ItemKind.SkillDocument && taughtSkill != null;
    }
}

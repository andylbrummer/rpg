# 戰之詳 — Combat System Deep Specification

> 兵者，詭道也。然程式之戰，必須確定無疑。

## 1. 戰鬥空間座標

雖無網格，距離帶即空間：

```
[  Party  ]          [  Enemy Groups  ]
 Front Row ──Melee──► Group A (melee)
 Back Row  ──Short──► Group B (ranged)
           ──Long──► Group C (artillery)
```

- Party Front Row 恆在 Melee
- Party Back Row 恆在 Short（敵人近戰可「跨排」條件見下）
- 敵人群組起始位置由遭遇表定義
- 敵人可消耗行動改變自身群組之距離帶

## 2. 跨排規則（Row Penetration）

正常：敵人 Melee 攻擊僅能命中 Front Row。

**Front Row 全滅或撤退時**：敵人 Melee 可攻擊 Back Row。
**特殊敵人**：The Unaccounted 之「reach through」無視此前排保護。
**玩家對策**：Bonewarden Animator 召喚物可填補前排空缺（視為前排單位）。

## 3. 命中與傷害公式

```csharp
// 命中判定
bool Hit(Combatant attacker, Combatant defender, AbilityDef ability, GameRandom rng)
{
    var roll = rng.Roll(1, 20);
    var accuracy = attacker.EffectiveStats.Finesse + ability.AccuracyMod;
    var evasion = defender.EffectiveStats.Finesse + defender.EvasionMod;
    return roll + accuracy >= 10 + evasion;
}

// 傷害計算
int Damage(Combatant attacker, Combatant defender, AbilityDef ability, GameRandom rng)
{
    var baseDmg = ability.BaseDamage + attacker.EffectiveStats.Might * ability.MightScaling;
    var roll = rng.Roll(ability.DamageRange.Min, ability.DamageRange.Max);
    var armor = defender.EffectiveStats.Armor;
    var raw = (baseDmg + roll) - armor;

    foreach (var tag in ability.Tags)
    {
        if (defender.Resistances.Contains(tag)) raw = (int)(raw * 0.5);
        if (defender.Immunities.Contains(tag)) raw = 0;
    }

    return Math.Max(1, raw);
}

// 暴擊：roll == 20 → 傷害 ×1.5
```

## 4. 先攻細則

```csharp
int InitiativeRoll(Combatant c, GameRandom rng)
{
    return c.Speed + c.InitiativeMod + rng.Roll(-3, 3);
}
```

- 每回合開始時全體重骰
- 同值時：玩家優先於敵人；玩家間依隊伍順序
- 延遲行動（Wait）：本回合不動，下回合先攻 +5

## 5. 能力詳細結構

```csharp
public record AbilityDef(
    string Id,
    string Name,
    string Description,
    ActionType ActionType,      // Attack, Cast, etc.
    TargetType Target,          // Self, Ally, Enemy, AllyGroup, EnemyGroup, AllEnemies
    RangeBand[] ValidRanges,    // 可發動之距離帶
    ComponentCost[] Costs,      // 消耗元件
    Effect[] Effects,
    string[] Tags,              // ["necromantic","buff","fire","physical"]
    int AccuracyMod,            // 額外命中加值
    int MightScaling,           // 每點 Might 加多少傷害
    DamageRange DamageRange,    // 骰子範圍
    int Cooldown,               // 冷卻回合數
    int ChargeTime              // 詠唱回合數（0 = 即時）
);
```

### Phase 1 能力清單（示例）

| ID | 職業 | 名稱 | 消耗 | 效果 | 標籤 |
|---|---|---|---|---|---|
| `bone-shard` | Bonewarden | Bone Shard | 2 骨片 | 單體 6-10 dmg | necromantic |
| `tithe-link` | Bonewarden | Tithe Link | 3 骨片 | 全體 +2 armor 2 回合 | necromantic, buff |
| `breaker-strike` | Stillblade | Breaker Strike | 無 | 單體 8-12 dmg，50% 消去敵人 buff | physical |
| `warden-stance` | Stillblade | Warden Stance | 無 | 自身 +5 threat，傷害減半 2 回合 | physical |
| `cauterize` | Cauterist | Cauterize | 1 烙療 | 單體回 8-12 HP | fire, heal |
| `flashfire` | Cauterist | Flashfire | 2 烙療 | 全體敵 4-6 fire dmg | fire |
| `cheap-shot` | Hollow | Cheap Shot | 無 | 單體 5-8 dmg，背排時 +4 | physical |
| `fade` | Hollow | Fade | 無 | 自身隱形 1 回合（不可被選為目標） | stealth |

## 6. 狀態效果系統

```csharp
public record StatusEffect(
    string Id,
    string Name,
    int RemainingDuration,   // 回合數，-1 = 永久（需特定條件解除）
    int Potency,
    string[] Tags,           // "poison", "bleed", "stun", "dread"
    Trigger[] Triggers       // 每回合/受擊時觸發
);

public enum TriggerType
{
    OnTurnStart,   // 回合開始時
    OnTurnEnd,     // 回合結束時
    OnHitTaken,    // 受擊時
    OnDeath,       // 死亡時（用於 reassemble）
    OnDispel,      // 被驅散時
}
```

### 標準狀態

| ID | 名稱 | 持續 | 效果 | 解除 |
|---|---|---|---|---|
| `bleed` | 流血 | 3 | 每回合結束 -3 HP | Cauterist Purify |
| `stun` | 暈眩 | 1 | 跳過下回合 | 回合結束自動解除 |
| `poison` | 中毒 | 4 | 每回合開始 -2 HP | Cauterist Purify |
| `burn` | 燃燒 | 3 | 每回合結束 -4 HP，Spread 機率 | Cauterist Purify |
| `shield` | 護盾 | 2 | 吸收下次 10 dmg | 吸收後解除 |
| `dread` | 恐懼 | ∞ | 全屬性 -3 | 擊殺來源敵人 |

## 7. 敵人設計詳細

### Bloom Spawnling（Phase 1 Bloom 敵人）

```json
{
  "id": "bloom-spawnling",
  "name": "Bloom Spawnling",
  "category": "bloom",
  "stats": { "hp": 22, "speed": 8, "accuracy": 2, "evasion": 3, "armor": 1 },
  "ai": "bloom_random",
  "abilities": ["bloom-claw", "mutate"],
  "resistances": ["necromantic"],
  "immunities": ["poison"],
  "loot": "bloom-trash"
}
```

- `bloom-claw`：Melee，5-8 dmg，30% 附加 `poison`
- `mutate`：30% 機率於回合開始時觸發，獲得隨機 buff（+speed 或 +armor）

### Bureau Scout（Phase 1 Soldier 敵人）

```json
{
  "id": "bureau-scout",
  "name": "Bureau Scout",
  "category": "soldier",
  "stats": { "hp": 28, "speed": 6, "accuracy": 4, "evasion": 2, "armor": 3 },
  "ai": "soldier_tactical",
  "abilities": ["sword-strike", "tactical-retreat"],
  "resistances": [],
  "immunities": [],
  "loot": "bureau-trash"
}
```

- 優先攻擊 HP 最低玩家
- HP < 9 時嘗試 `tactical-retreat`（移動至 Short band）
- 若已在 Short 且 HP < 9，則嘗試 Flee（50% 機率成功）

### Malfunctioning Construct（Phase 1 Construct 敵人）

```json
{
  "id": "malfunctioning-construct",
  "name": "Malfunctioning Construct",
  "category": "construct",
  "stats": { "hp": 40, "speed": 4, "accuracy": 3, "evasion": 0, "armor": 5 },
  "ai": "construct_guard",
  "abilities": ["slam", "overcharge"],
  "resistances": ["necromantic", "poison"],
  "immunities": ["bleed", "burn"],
  "loot": "construct-trash"
}
```

- `slam`：Melee，8-12 dmg
- `overcharge`：每 3 回合一次，全體 6-10 lightning dmg
- 弱點：Fieldwright 可消耗 1 Engine Charge 使其下回合 Stun

## 8. 協同詳細（Phase 1.5+）

雖 Phase 1 無協同，引擎預埋結構：

```csharp
public record SynergyDef(
    string Id,
    string AbilityA,
    string AbilityB,
    SynergyTrigger Trigger,
    Effect BonusEffect,
    string HintText,
    string[] RequiredConditions   // e.g. ["enemy-has-tag:bloom"]
);

public enum SynergyTrigger
{
    SameRound,        // A+B 同回合
    Sequential,       // A 後立刻 B（相鄰行動）
    OnSameTarget,     // A+B 同目標
}
```

協同註冊表為靜態只讀字典，啟動時載入：
```csharp
public static class SynergyRegistry
{
    public static readonly ImmutableDictionary<(string,string), SynergyDef> ByPair;
    static SynergyRegistry()
    {
        var builder = ImmutableDictionary.CreateBuilder<(string,string), SynergyDef>();
        foreach (var syn in Content.Synergies)
        {
            var key = OrderIndependent(syn.AbilityA, syn.AbilityB);
            builder[key] = syn;
        }
        ByPair = builder.ToImmutable();
    }
}
```

## 9. 逃跑機制

```csharp
FleeResult TryFlee(CombatState state, GameRandom rng)
{
    var enemySpeed = state.Combatants.Where(c => !c.IsPlayer).Max(c => c.Speed);
    var playerSpeed = state.Combatants.Where(c => c.IsPlayer).Max(c => c.Speed);
    var roll = rng.Roll(1, 20) + playerSpeed - enemySpeed;

    if (roll >= 12) return FleeResult.Success;
    if (roll >= 8) return FleeResult.Partial; // 撤退至 Long band，下回合可再試
    return FleeResult.Failed; // 浪費回合，敵人獲得 free attack
}
```

## 10. 戰鬥日誌結構

```json
{
  "round": 3,
  "actorId": "uuid",
  "actionType": "Cast",
  "abilityId": "flashfire",
  "targetId": "enemy-uuid",
  "damage": 12,
  "wasCrit": false,
  "synergyTriggered": null,
  "tags": ["fire"],
  "statusApplied": ["burn"],
  "statusRemoved": [],
  "rangeChanged": null
}
```

日誌用於：
- 戰鬥 UI 顯示
- 快照測試斷言
- Phase 3 戰役尾聲回顧

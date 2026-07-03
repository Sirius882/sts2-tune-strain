# 集谐（Tune Strain）系统简述

给怪物上集谐·偏移，然后处决它，这样你对它的伤害就会变高。集谐·偏移的层数越高、手里有集谐响应的牌越多，增伤幅度越大。

# 集谐系统详述

1. 基本概念
    集谐·偏移是玩家通过各种方式附加在怪物身上的一种debuff。单纯的集谐·偏移没有任何效果。集谐·偏移可以叠层，目前只有1层和2层的效果有区别，3层及以上与2层相同
    带有集谐·偏移的怪物被谐度破坏（以下简称“集谐处决”）时，除原本的处决效果外，会清空集谐·偏移，并将其转化为等量的集谐·干涉
    拥有集谐·干涉的目标不能被附加集谐·偏移。
    带有集谐·干涉的怪物会变得更容易受到伤害
    集谐响应是附加在卡牌上的标记，本身没有效果。集谐响应分为卡牌自带的永久的，和战斗中附加的临时的。临时集谐响应在集谐处决触发时或每场战斗结束时清除。

2. 集谐处决
    集谐处决会清空目标身上的集谐·偏移，附加等量的集谐·干涉（和一个隐藏power用于计量集谐·干涉的持续回合数，该隐藏power每回合扣一层，扣到0时清除集谐·干涉power和自己）。集谐处决的触发者会获得等值于清除的集谐·偏移层数的力量，对每次集谐处决，最多不超过2点；对每场战斗，最多共计不超过4点。
    集谐处决触发时，计算触发者抽牌堆、弃牌堆和手牌中，带有集谐响应标记的牌的总数。计算完毕后，清除所有临时的集谐响应标记。考察此时玩家拥有集谐响应power的情况：若无或层数少于该总数，用该总数覆盖层数，并刷新power的持续回合数（“集谐响应power的持续回合数”也是一个隐藏的power，每回合扣一层，扣到0时清除集谐响应power和自己）。若层数多于该总数，只刷新持续回合数。

    2.2 集谐响应度 tsResponse()
        集谐响应power发挥作用前，要先折算为集谐响应度
        集谐响应度并非简单等于power层数，而是采用超额累进计算。
            当集谐响应power层数不大于30时，集谐响应度 = 集谐响应标记总数
            当集谐响应power层数大于30而不大于50时，超出30的部分只按一半计入集谐响应度（不用取整）
            当集谐响应power层数大于50而不大于100时，超出50的的部分只按四分之一计入集谐响应度（不用取整）
            当集谐响应power层数大于100时，超出100的部分不计入集谐响应度
        超额累进计算的目的是限制卡组巨大时，因指数函数增长过快，伤害容易过高的问题。灵感来自个人所得税超额累进税率。
        为方便算法表述，将以上计算规则定义为函数'def tsResponse(tsrPower)'
        集谐响应度每次用到时临时计算，不保存。
    
    2.3 集谐易伤 tsDmgAdvRatio()
        集谐易伤是集谐·干涉存续期间，玩家对怪物造成的伤害的增加比例。如果怪物身上没有集谐·干涉，则无集谐易伤，无论是否有集谐响应power
        集谐易伤在力量、虚弱、易伤等原版效果结算完成后最后结算，不与其他伤害增减比率相加减。
        集谐易伤只作用于直接伤害。聚爆引爆、熔解、谐度破坏本身的伤害都不受集谐易伤影响。
        集谐易伤取决于集谐·干涉层数（即处决前的集谐·偏移层数，记为tsBias）和集谐响应度（记为tsRes）
        集谐易伤的规则如下：
        def tsBias2Base(tsBias)
            if tsBias == 1:
                return 1.15
            elif tsBias == 2:
                return 1.2
            else:
                return 1.2
            # 为以后可能设计tsBias > 2有更多别的效果留出空间。实际实现时，此处应留出API
        
        def tsDmgAdvRatio(tsBias,tsRes)
            a = tsBias2Base(tsBias)**tsRes
            return 2*a/100
        最终得到：
        dmgAdvRatio = tsDmgAdvRatio(怪物1的tsBias,tsResponse(玩家1的集谐响应power层数))
        finalDmg = originalDmg*(1+dmgAdvRatio)
        # 为降低耦合度并留出设计空间，不在tsDmgAdvRatio()的定义中引用tsResponse()。实际实现时，此处应留出API
    集谐·干涉存续期间，附有集谐·干涉的怪物不能被附加集谐·偏移。存在复数个怪物时，不附有集谐·干涉的怪物不受影响，可以继续被附加集谐·偏移。
    集谐·干涉和集谐响应power被创建后，初始持续3个回合。其中，复数个怪物的场景下，集谐响应power的持续回合数有可能被刷新；而因为集谐·干涉存续期间不能附加集谐·偏移，不可能触发集谐处决，故集谐·干涉的持续回合数无刷新的情况。

3. 共鸣模态·集谐
    不同角色可以有自己的共鸣模态·集谐效果，并不通用。
    达妮娅的共鸣模态·集谐效果是，处于该模态时，在计算集谐增伤时，按2倍采用集谐响应power层数，即：
        if tune_strain_mode:
            dmgAdvRatio = tsDmgAdvRatio(怪物1的tsBias,tsResponse(2*达妮娅的集谐响应power层数))
    注意达妮娅共鸣模态·集谐的实现不能直接写死，而是在集谐系统中留出API，达妮娅的共鸣模态·集谐使用该API，以实现达妮娅与集谐系统的解耦

# 开发者 API

本节面向其他角色 Mod 或机制 Mod 的开发者。`tune_strain` 是一个机制库 Mod，提供集谐·偏移、集谐·干涉、集谐响应关键词与集谐处决后的统一结算。

## 依赖配置

在你的 Mod 描述文件中声明依赖：

```json
{
    "dependencies": [
        { "id": "BaseLib" },
        { "id": "aemeath-ww" },
        { "id": "tune_strain" }
    ]
}
```

在 `.csproj` 中引用 `tune_strain.dll`。路径按你的项目结构调整：

```xml
<Reference Include="tune_strain">
    <HintPath>..\tune_strain\.godot\mono\temp\bin\Debug\tune_strain.dll</HintPath>
</Reference>
```

使用命名空间：

```csharp
using TuneStrain;
```

## 卡牌自带集谐响应

如果一张牌天生带有集谐响应，把 `TuneStrainKeywords.TuneStrainResponse` 放进该牌的 `CanonicalKeywords`。这是长期关键词，不会在集谐处决或战斗结束时被清掉。

```csharp
public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { TuneStrainKeywords.TuneStrainResponse };
```

不要用 `TuneStrainState.AddTemporaryResponse` 实现卡牌自带响应。那个 API 只用于战斗中临时给牌贴响应。

## 附加集谐·偏移

使用 `TryAddBias` 给敌人附加集谐·偏移。调用方必须 `await`。

```csharp
await TuneStrainState.TryAddBias(target, 1, Owner.Creature, this);
```

返回值表示是否成功附加。以下情况会返回 `false`：层数不大于 0、目标已死亡、目标已有集谐·干涉。

读取当前偏移层数：

```csharp
int bias = TuneStrainState.GetBias(target);
```

判断目标是否处于集谐·干涉：

```csharp
bool hasInterference = TuneStrainState.HasInterference(target);
```

## 临时集谐响应

有些卡牌会在本场战斗中临时给牌附加集谐响应。使用 `AddTemporaryResponse`，传入玩家和牌组中的永久牌。

```csharp
TuneStrainState.AddTemporaryResponse(Owner, deckCard);
```

该 API 会给这张永久牌以及本场战斗中对应的所有副本贴上 `TuneStrainResponse` 关键词。临时响应会在集谐处决触发时或战斗结束时自动清理。

判断一张牌是否已被临时响应机制标记：

```csharp
bool alreadyTemporary = TuneStrainState.HasTemporaryResponse(card);
```

常见用法是在选牌时排除已经有响应的牌：

```csharp
card => !card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)
        && !TuneStrainState.HasTemporaryResponse(card)
```

## 响应 power 与响应度

读取玩家当前集谐响应 power 的原始层数：

```csharp
int layers = TuneStrainState.GetResponsePowerAmount(player.Creature);
```

读取已经过超额累进折算、并应用角色倍率后的集谐响应度：

```csharp
double responseDegree = TuneStrainState.GetResponseDegree(player.Creature);
```

通常角色 Mod 不需要自己计算伤害倍率；集谐·干涉的伤害修正由 `tune_strain` 的 power 和补丁统一处理。

## 注册响应度倍率

角色 Mod 如果有自己的“共鸣模态·集谐”或类似状态，可以注册响应 power 层数倍率。倍率会先作用于原始响应 power 层数，再进入超额累进折算。

```csharp
private IDisposable? _tuneStrainMultiplier;

_tuneStrainMultiplier = TuneStrainState.RegisterResponseDegreeMultiplier(creature =>
{
        return IsMyTuneStrainModeActive(creature) ? 2.0 : 1.0;
});
```

状态结束或 Mod 卸载时释放句柄：

```csharp
_tuneStrainMultiplier?.Dispose();
_tuneStrainMultiplier = null;
```

每个回调都应该只返回倍率，不要在回调里修改游戏状态。单个倍率回调抛异常时会被忽略，但仍建议保持实现简单稳定。

## 集谐处决结算

`tune_strain` 已经补丁接入 Aemeath 的谐度破坏和无条件谐度破坏：当 Aemeath 的谐度破坏成功结算后，会自动调用集谐处决逻辑。

一般情况下，其他 Mod 不需要手动调用 `ResolveRupture`。如果你实现了自己的“处决”入口，并希望它也触发集谐处决，可以调用：

```csharp
await TuneStrainState.ResolveRupture(target, applier, sourceCard);
```

注意：`ResolveRupture` 不检查 Aemeath 偏谐是否已满，也不检查是否满足谐度破坏条件。调用方必须自己保证触发时机正确。该方法会：

- 清空目标的集谐·偏移。
- 附加等量集谐·干涉和 3 回合持续计时。
- 给触发者力量奖励，每次最多 2 点，每场战斗最多 4 点。
- 统计触发者抽牌堆、手牌、弃牌堆中的集谐响应牌，更新集谐响应 power 和持续计时。
- 清除所有临时集谐响应标记。

## 伤害修正边界

集谐·干涉期间的增伤只作用于直接伤害。聚爆引爆、熔解、谐度破坏本身的伤害不会受到集谐易伤影响。

## 稳定性约定

- 公开入口集中在 `TuneStrainState` 与 `TuneStrainKeywords`。
- 写入型 API 只要返回 `Task`，调用方都应 `await`。
- 永久响应用 `CanonicalKeywords`，临时响应用 `AddTemporaryResponse`。
- 不要直接修改 `TuneStrainBiasPower`、`TuneStrainInterferencePower` 或 `TuneStrainResponsePower` 的层数；优先使用 API。

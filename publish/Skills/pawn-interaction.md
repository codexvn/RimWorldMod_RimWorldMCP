---
name: pawn-interaction
description: 角色交互与应急处理。在执行逮捕、救援、俘虏、剥除、强制穿戴、服食、床位设置等操作时激活。
---

# 角色交互与应急处理

## 工具概览

| 工具 | 用途 | 关键约束 |
|------|------|----------|
| `arrest_pawn` | 逮捕殖民者/访客 | 需要囚犯床 |
| `rescue_pawn` | 救援倒地友方 | 需要医疗床 |
| `capture_pawn` | 俘虏倒地敌人 | 需要囚犯床 |
| `strip_pawn` | 剥除目标衣物装备 | 目标可以是活体或尸体 |
| `force_dress` | 强制给殖民者穿衣 | 需要 doer 和 target 两个殖民者 |
| `ingest_item` | 服食药物/食物 | 殖民者自动走过去 |
| `pick_up_item` | 拾取物品到背包 | 支持指定数量 |
| `drop_equipment` | 丢弃当前装备的武器 | 无需目标参数 |
| `set_bed_owner_type` | 设置床为殖民者/囚犯/奴隶 | 切换囚犯床会检查殖民者有无床可用 |
| `set_temp_control` | 调整空调/加热器温度或开关 | 用 thing_id 或坐标定位 |
| `get_open_dialogs` | 列出所有弹框及选项 | 返回编号供选择 |
| `select_dialog_option` | 选择弹框选项 | dialog_index + option_index |
| `move_pawn` | 移动殖民者到指定坐标 | 通过 Goto Job |

## 角色操作参考

使用 `thingIDNumber` 精确定位角色，不要用名称字符串匹配。

### 逮捕流程
```
1. set_bed_owner_type(owner_type="prisoner") → 确保有囚犯床
2. arrest_pawn(doer_id, target_id) → 执行逮捕
3. 逮捕后目标变为囚犯，可使用 capture_pawn 流程管理
```

### 救援流程
```
1. find_pawn / get_tile_detail → 找到倒地友方
2. rescue_pawn(doer_id, target_id) → 执行救援
   - 自动送往最近的可用医疗床
   - 确保有殖民者类型的床可用
```

### 俘虏流程
```
1. set_bed_owner_type(owner_type="prisoner") → 确保有囚犯床
2. capture_pawn(doer_id, target_id) → 执行俘虏
   - 仅限倒地敌人
   - 俘虏后需安排看守（Warden 工作）
```

### 战后缴获
```
1. find_enemies → 确认战斗结束
2. allow_all_items → 允许缴获物品
3. strip_pawn(doer_id, target_id) → 剥除敌人尸体
4. haul_item → 搬运缴获装备到仓库
5. 用 get_recommended_weapon/get_recommended_apparel 评估升级
```

### 强制装备
```
1. get_recommended_apparel / get_recommended_weapon → 找到最佳装备
2. equip_pawn(colonist_id, thing_id) → 普通装备
3. force_dress(doer_id, target_id, thing_id) → 强制给他人穿衣
   - doer 去拿衣物，给 target 穿上
   - 用于殖民者不自动更换衣物时
```

### 服食药物
```
1. get_colonist_health → 确认需求（感染、心情崩溃等）
2. ingest_item(colonist_id, thing_id) → 服食
   - 盘诺西林: 预防感染
   - 精神茶: 提升心情
   - 清醒药: 临时提升效率
```

## 床位管理

### 床位类型切换
```
set_bed_owner_type(pos_x, pos_y, owner_type)
```
- `colonist` — 普通殖民者床
- `prisoner` — 囚犯床（需要独立房间）
- `slave` — 奴隶床（仅 Ideology DLC）

切换为 prisoner 时会检查是否导致殖民者无床可用，传 `force=true` 跳过。

## 温度控制

```
set_temp_control(thing_id=xxx, target_temp=21, power_on=true)
```
- 空调: 目标温度设为制冷阈值（如 21°C）
- 加热器: 目标温度设为制热阈值
- 冷库: -5°C 以下冻结食物
- 不传 target_temp 则只切换电源

## 弹框交互

```
1. get_open_dialogs → 获取弹框列表和选项
2. select_dialog_option(dialog_index, option_index) → 选择
```

常见弹框类型：
- `FloatMenu` — 右键菜单（制造、搬运等选项）
- `Dialog_MessageBox` — 确认对话框（按钮 B/C/A）
- `Dialog_NodeTree` — 事件选项（任务/交易等）
- `Dialog_GiveName` — 命名对话框

## 移动控制
- `move_pawn(colonist_name, pos_x, pos_y)` — 移动到指定坐标
- `move_camera(pos_x, pos_y)` — 移动视角
- 战斗时先征召 (`draft_pawn`)，再移动部署

## 相关 Skill
- 战斗 → [[combat-preparation]]
- 医疗 → [[medical-care]]
- 装备优化 → [[equipment-optimization]]
- 殖民地管理 → [[colony-management]]

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace RimWorldMCP.Tools
{
    public class Tool_TradeExecute : ITool
    {
        public string Name => "trade_execute";
        public string Description => "与商船或派系执行交易。通过 ship_name 指定商船，或 faction_name 指定派系（创建虚拟商船进行虚空贸易）。定价使用游戏内置交易系统。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                ship_name = new { type = "string", description = "商船名称（与 faction_name 二选一）" },
                faction_name = new { type = "string", description = "派系名称（与 ship_name 二选一）" },
                trader_kind = new { type = "string", description = "商船类型（可选，不传则列出派系可用类型）" },
                sell = new
                {
                    type = "array",
                    description = "出售给商船的物品列表",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            item = new { type = "string", description = "物品名称（匹配 Label/defName，模糊匹配）" },
                            count = new { type = "integer", description = "出售数量" }
                        }
                    }
                },
                buy = new
                {
                    type = "array",
                    description = "从商船购买的物品列表",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            item = new { type = "string", description = "物品名称（匹配 Label/defName，模糊匹配）" },
                            count = new { type = "integer", description = "购买数量" }
                        }
                    }
                }
            },
            required = new[] { "ship_name" }
        });

        private static readonly PropertyInfo _costForSource = typeof(Tradeable).GetProperty("CurTotalCurrencyCostForSource")
            ?? throw new Exception("CurTotalCurrencyCostForSource not found");
        private static readonly PropertyInfo _allTradeablesProp = typeof(TradeDeal).GetProperty("AllTradeables")
            ?? throw new Exception("AllTradeables not found");

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return ToolResult.Error("缺少参数");
            string shipName = "";
            string factionName = "";
            if (args.Value.TryGetProperty("ship_name", out var jSn))
                shipName = jSn.GetString() ?? "";
            if (args.Value.TryGetProperty("faction_name", out var jFn))
                factionName = jFn.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(shipName) && string.IsNullOrWhiteSpace(factionName))
                return ToolResult.Error("缺少必填参数: ship_name 或 faction_name");

            string traderKindFilter = "";
            if (args.Value.TryGetProperty("trader_kind", out var jTk))
                traderKindFilter = jTk.GetString() ?? "";

            var sellList = new List<(string item, int count)>();
            if (args.Value.TryGetProperty("sell", out var jSell) && jSell.ValueKind == JsonValueKind.Array)
                foreach (var si in jSell.EnumerateArray())
                {
                    string s = si.TryGetProperty("item", out var si1) ? si1.GetString() ?? "" : "";
                    int c = si.TryGetProperty("count", out var si2) ? si2.GetInt32() : 0;
                    if (!string.IsNullOrWhiteSpace(s) && c > 0) sellList.Add((s, c));
                }

            var buyList = new List<(string item, int count)>();
            if (args.Value.TryGetProperty("buy", out var jBuy) && jBuy.ValueKind == JsonValueKind.Array)
                foreach (var bi in jBuy.EnumerateArray())
                {
                    string s = bi.TryGetProperty("item", out var bi1) ? bi1.GetString() ?? "" : "";
                    int c = bi.TryGetProperty("count", out var bi2) ? bi2.GetInt32() : 0;
                    if (!string.IsNullOrWhiteSpace(s) && c > 0) buyList.Add((s, c));
                }

            if (sellList.Count == 0 && buyList.Count == 0)
                return ToolResult.Error("至少需要 sell 或 buy 之一");

            return await McpCommandQueue.DispatchAsync(() =>
            {
                Map map = Find.CurrentMap;
                if (map == null) return ToolResult.Error("没有当前地图");

                ITrader trader = null!;
                bool virtualTrader = false;
                string traderLabel = "";

                try
                {

                    if (!string.IsNullOrWhiteSpace(shipName))
                    {
                        var ship = map.passingShipManager.passingShips.OfType<TradeShip>()
                            .FirstOrDefault(s => !s.Departed && s.CanTradeNow
                                && s.name.ToLowerInvariant().Contains(shipName.ToLowerInvariant()));
                        if (ship == null)
                            return ToolResult.Error($"找不到商船: {shipName}");
                        trader = ship;
                        traderLabel = ship.name;
                    }
                    else
                    {
                        var faction = Find.FactionManager.AllFactionsVisible
                            .FirstOrDefault(f => f.Name.ToLowerInvariant().Contains(factionName.ToLowerInvariant())
                                && !f.IsPlayer && !f.temporary);
                        if (faction == null)
                            return ToolResult.Error($"找不到派系: {factionName}");
                        if (faction.PlayerRelationKind == FactionRelationKind.Hostile)
                            return ToolResult.Error($"{faction.Name} 与你是敌对关系，无法贸易");

                        // 列出派系可用商船类型
                        var allKinds = (faction.def.orbitalTraderKinds ?? new List<TraderKindDef>())
                            .Concat(faction.def.caravanTraderKinds ?? new List<TraderKindDef>())
                            .Where(k => k != null)
                            .Distinct()
                            .ToList();

                        if (allKinds.Count == 0)
                            return ToolResult.Error($"{faction.Name} 没有可用的贸易类型");

                        // 指定类型 → 模糊匹配
                        TraderKindDef? traderKind = null;
                        if (!string.IsNullOrWhiteSpace(traderKindFilter))
                        {
                            traderKind = allKinds.FirstOrDefault(k =>
                                (k.label?.ToLowerInvariant().Contains(traderKindFilter.ToLowerInvariant()) ?? false)
                                || (k.defName?.ToLowerInvariant().Contains(traderKindFilter.ToLowerInvariant()) ?? false));
                            if (traderKind == null)
                                return ToolResult.Error($"{faction.Name} 没有 '{traderKindFilter}' 类型的商船。可用: {string.Join(", ", allKinds.Select(k => k.label))}");
                        }
                        else if (sellList.Count > 0 || buyList.Count > 0)
                        {
                            // 有买卖请求但未指定类型 → 自动选第一个
                            traderKind = allKinds[0];
                        }
                        else
                        {
                            // 仅探索 → 列出类型
                            return ToolResult.Success($"{faction.Name} 可用商船类型: {string.Join(", ", allKinds.Select(k => $"{k.label}({k.defName})"))}");
                        }

                        var virtShip = new TradeShip(traderKind, faction);
                        virtShip.GenerateThings();
                        virtShip.PassingShipTick();
                        map.passingShipManager.AddShip(virtShip);
                        trader = virtShip;
                        traderLabel = $"{faction.Name}/{traderKind.label}";
                        virtualTrader = true;
                    }

                    var pawn = PawnsFinder.AllMaps_FreeColonistsSpawned
                        .FirstOrDefault(p => p.health.capacities.CapableOf(PawnCapacityDefOf.Talking));
                    if (pawn == null) return ToolResult.Error("没有能说话的殖民者");

                    TradeSession.trader = trader;
                    TradeSession.playerNegotiator = pawn;
                    var deal = new TradeDeal();
                    var tradeables = (List<Tradeable>)_allTradeablesProp.GetValue(deal);

                    var log = new StringBuilder();
                    int sellValue = 0, buyValue = 0;

                    foreach (var (itemName, count) in sellList)
                    {
                        var t = FindTradeable(tradeables, itemName, true);
                        if (t == null) { log.AppendLine($"找不到可售物品: {itemName}"); continue; }
                        int max = t.CountToTransferToSource;
                        if (max <= 0) { log.AppendLine($"{itemName}: 无可售库存"); continue; }
                        int actual = Math.Min(count, max);
                        t.ForceToSource(actual);
                        int v = (int)Math.Round((float)_costForSource.GetValue(t));
                        sellValue += v;
                        log.AppendLine($"售 {t.Label} x{actual} → +{v} 银");
                    }

                    foreach (var (itemName, count) in buyList)
                    {
                        var t = FindTradeable(tradeables, itemName, false);
                        if (t == null) { log.AppendLine($"商船没有: {itemName}"); continue; }
                        int max = t.CountToTransferToDestination;
                        if (max <= 0) { log.AppendLine($"{itemName}: 商船库存不足"); continue; }
                        int actual = Math.Min(count, max);
                        t.ForceToDestination(actual);
                        int v = (int)Math.Round((float)_costForSource.GetValue(t));
                        buyValue += Math.Abs(v);
                        log.AppendLine($"购 {t.Label} x{actual} → -{Math.Abs(v)} 银");
                    }

                    int net = sellValue - buyValue;
                    log.AppendLine($"净额: {net:+0;-#} 银");

                    if (sellValue == 0 && buyValue == 0)
                    {
                        ResetSession(virtualTrader ? trader as TradeShip : null);
                        return ToolResult.Error("没有成功匹配到任何物品");
                    }

                    bool ok = deal.TryExecute(out bool actuallyTraded);
                    ResetSession(virtualTrader ? trader as TradeShip : null);

                    if (!ok) return ToolResult.Error("交易执行失败");
                    if (!actuallyTraded) return ToolResult.Error("交易未产生实际交换（可能资金不足）");
                    log.AppendLine($"交易完成，对方: {traderLabel}");
                    return ToolResult.Success(log.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    ResetSession(virtualTrader ? trader as TradeShip : null);
                    return ToolResult.Error($"交易失败: {ex.Message}");
                }
            });
        }

        private static void ResetSession(TradeShip? virtualShip = null)
        {
            TradeSession.trader = null!;
            TradeSession.playerNegotiator = null!;
            if (virtualShip != null)
            {
                virtualShip.Depart();
            }
        }

        private static Tradeable? FindTradeable(List<Tradeable> list, string name, bool playerOwned)
        {
            foreach (var t in list)
            {
                if (!t.TraderWillTrade) continue;
                var label = (t.Label?.ToLowerInvariant() ?? "");
                var defName = (t.ThingDef?.defName?.ToLowerInvariant() ?? "");
                var nl = name.ToLowerInvariant();
                if (label.Contains(nl) || defName.Contains(nl))
                {
                    // 检查方向：playerOwned → 玩家有库存可卖；!playerOwned → 商船有库存可买
                    if (playerOwned && t.CountToTransferToSource > 0) return t;
                    if (!playerOwned && t.CountToTransferToDestination > 0) return t;
                }
            }
            return null;
        }
        public (int minX, int minZ, int maxX, int maxZ)? GetTargetRange(JsonElement? args) => null;
    }
}

using System.Collections.Generic;

namespace ImmersiveCrafting
{
    /// <summary>All texts of the mod, Russian and English. Keys are used in Settings.cs with the "IMCR_" prefix.</summary>
    internal static class Texts
    {
        /// <summary>Key -> (Russian, English).</summary>
        public static readonly Dictionary<string, (string ru, string en)> All = new()
        {
            ["SECTION_STATIONS"] = ("Верстаки, горн и огонь", "Workbenches, forge and fire"),
            ["STATIONS"] = ("Инструменты рядом", "Tools lying nearby"),
            ["STATIONS_DESC"] = (
                "Да — инструменты, которые лежат рядом с верстаком, верстаком для патронов, верстаком для мебели, горном или огнём " +
                "(крафт у огня), можно использовать при крафте там, как будто они у вас в инвентаре. Инструмент остаётся на месте " +
                "и изнашивается как обычно. Из нескольких одинаковых берётся верхний, на одной высоте — дальний.",
                "Yes — tools lying next to a workbench, ammo workbench, furniture workbench, forge or fire (crafting at a fire) " +
                "can be used for crafting there, as if they were in your inventory. The tool stays where it is and wears as usual. " +
                "Of several alike, the highest is used, at the same height the farthest."),
            ["MATERIALS"] = ("    Материалы рядом", "    Materials lying nearby"),
            ["MATERIALS_DESC"] = (
                "Да — материалы, которые лежат рядом, тоже идут в крафт: штучные, по весу, порох, соль, керосин. " +
                "Сначала тратится то, что у вас в инвентаре, потом лежащее рядом: сверху вниз (верх горки, стол, пол, под столом — " +
                "в последнюю очередь), на одной высоте — сначала дальнее.",
                "Yes — materials lying nearby are used for crafting too: pieces, by weight, gunpowder, salt, lamp oil. " +
                "What you carry is used first, then what lies around: from the top down (top of a pile, the table, the floor, " +
                "under the table last), at the same height the farthest first."),
            ["REPAIR"] = ("    Ремонт у верстака", "    Repairs at a workbench"),
            ["REPAIR_DESC"] = (
                "Да — когда вы ремонтируете вещь, стоя у верстака, верстака для патронов, верстака для мебели или горна (неважно, открыли " +
                "ремонт у верстака или из инвентаря), инструменты и материалы, которые лежат там, тоже можно использовать. " +
                "Инструменты предлагаются после ваших, материалы тратятся после ваших, сверху вниз.",
                "Yes — when you repair something standing at a workbench, ammo workbench, furniture workbench or forge (whether you " +
                "opened the repair at the bench or from the inventory), tools and materials lying there can be used too. " +
                "Tools are offered after yours, materials are used up after yours, from the top down."),
            ["SMALL_CONTAINERS"] = ("    Малые контейнеры у верстака", "    Small containers at the workbench"),
            ["SMALL_CONTAINERS_DESC"] = (
                "Да — инструменты и материалы в малых контейнерах рядом с верстаком (до 5 кг, например ящик самого верстака) " +
                "тоже идут в крафт. Только открытые и уже осмотренные. Сначала тратится то, что лежит открыто. У огня не действует.",
                "Yes — tools and materials in small containers next to the workbench (up to 5 kg, like the workbench's own box) " +
                "are used for crafting too. Only unlocked and already searched ones. What lies openly is used first. Not at a fire."),
            ["STATION_RADIUS"] = ("    Расстояние у верстака (м)", "    Distance at a workbench (m)"),
            ["STATION_RADIUS_DESC"] = (
                "Насколько далеко от края верстака может лежать предмет.",
                "How far from the edge of the workbench an item may lie."),
            ["FORGE_RADIUS"] = ("    Расстояние у горна (м)", "    Distance at a forge (m)"),
            ["FORGE_RADIUS_DESC"] = (
                "Насколько далеко от края горна может лежать инструмент или материал. Горн большой, по умолчанию 2,5 м.",
                "How far from the edge of a forge a tool or material may lie. A forge is big, 2.5 m by default."),
            ["FIRE_RADIUS"] = ("    Расстояние у огня (м)", "    Distance at a fire (m)"),
            ["FIRE_RADIUS_DESC"] = (
                "Для крафта у огня (факел, брикет): насколько далеко от края огня может лежать предмет.",
                "For crafting at a fire (torch, fire log): how far from the edge of the fire an item may lie."),

            ["SECTION_ICE"] = ("Рыбацкие лунки", "Ice fishing holes"),
            ["ICE"] = ("Инструменты рядом с лункой", "Tools next to the fishing hole"),
            ["ICE_DESC"] = (
                "Да — топор, монтировку и другие инструменты, которые лежат рядом, можно выбрать, чтобы прорубить или расчистить лунку. " +
                "Сама рыбалка не меняется: снасти, наживка и жерлицы нужны в инвентаре.",
                "Yes — a hatchet, prybar or another tool lying nearby can be chosen to cut or clear the hole. " +
                "Fishing itself is unchanged: tackle, bait and tip-ups must be in your inventory."),
            ["ICE_RADIUS"] = ("    Расстояние (м)", "    Distance (m)"),
            ["ICE_RADIUS_DESC"] = (
                "Насколько далеко от лунки может лежать инструмент. Для новой лунки считается от вас.",
                "How far from the hole a tool may lie. For a new hole it is measured from you."),
        };
    }
}

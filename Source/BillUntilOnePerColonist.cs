﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using RimWorld;
using Harmony;
using TD.Utilities;

namespace TD_Enhancement_Pack
{
	[DefOf]
	public static class RepeatModeDefOf
	{
		public static BillRepeatModeDef TD_ColonistCount;
		public static BillRepeatModeDef TD_XPerColonist;
		public static BillRepeatModeDef TD_WithSurplusIng;
	}

	public static class Extensions
	{
		public static int TargetCount(this Bill_Production bill)
		{
			return 
				bill.repeatMode == RepeatModeDefOf.TD_ColonistCount ? bill.Map.mapPawns.ColonistCount + bill.targetCount :
				bill.repeatMode == RepeatModeDefOf.TD_XPerColonist ? bill.Map.mapPawns.ColonistCount * bill.targetCount : bill.targetCount;
		}
		public static int UnpauseWhenYouHave(this Bill_Production bill)
		{
			return 
				bill.repeatMode == RepeatModeDefOf.TD_ColonistCount ? bill.Map.mapPawns.ColonistCount + bill.unpauseWhenYouHave :
				bill.repeatMode == RepeatModeDefOf.TD_XPerColonist ? bill.Map.mapPawns.ColonistCount * bill.unpauseWhenYouHave : bill.unpauseWhenYouHave;
		}
		public static int IngredientCount(this Bill_Production bill)
		{
			return bill.Map.resourceCounter.GetCountFor(bill.recipe.ingredients.First().filter.BestThingRequest);
		}

		public static int GetCountFor(this ResourceCounter res, ThingRequest request)
		{
			if (request.singleDef != null)
				return res.GetCount(request.singleDef);
			else
				return res.GetCountIn(request.group);
		}
	}

	[HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.RepeatInfoText), MethodType.Getter)]
	class RepeatInfoText_Patch
	{
		//public string RepeatInfoText
		public static bool Prefix(ref string __result, Bill_Production __instance)
		{
			if (__instance.repeatMode == RepeatModeDefOf.TD_ColonistCount)
			{
				__result = $"{__instance.recipe.WorkerCounter.CountProducts(__instance)}/({__instance.Map.mapPawns.ColonistCount}+{__instance.targetCount})";
				return false;
			}
			if (__instance.repeatMode == RepeatModeDefOf.TD_XPerColonist)
			{
				__result = $"{__instance.recipe.WorkerCounter.CountProducts(__instance)}/({__instance.Map.mapPawns.ColonistCount * __instance.targetCount})";
				return false;
			}
			if (__instance.repeatMode == RepeatModeDefOf.TD_WithSurplusIng)
			{
				__result = $"{__instance.IngredientCount()} > {__instance.targetCount}";
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ShouldDoNow))]
	class ShouldDoNow_Patch
	{
		//public override bool ShouldDoNow()
		public static bool Prefix(ref bool __result, Bill_Production __instance)
		{
			if (__instance.repeatMode == RepeatModeDefOf.TD_ColonistCount || 
				__instance.repeatMode == RepeatModeDefOf.TD_XPerColonist)
			{
				int products = __instance.recipe.WorkerCounter.CountProducts(__instance);
				int targetCount = __instance.TargetCount();
				if (__instance.pauseWhenSatisfied && products >= targetCount)
				{
					__instance.paused = true;
				}
				if (products <= __instance.UnpauseWhenYouHave() || !__instance.pauseWhenSatisfied)
				{
					__instance.paused = false;
				}
				__result = !__instance.paused && products < targetCount;
				return false;
			}
			if (__instance.repeatMode == RepeatModeDefOf.TD_WithSurplusIng)
			{
				__result = __instance.IngredientCount() > __instance.targetCount;
				return false;
			}
			return true;
		}
	}
	
	[HarmonyPatch(typeof(Bill_Production), "DoConfigInterface")]
	class DoConfigInterface_Patch
	{
		//protected override void DoConfigInterface(Rect baseRect, Color baseColor)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return OrColonistCount_Transpiler.Transpiler(instructions);
		}
	}

	[HarmonyPatch(typeof(Bill_Production), "CanUnpause")]
	class CanUnpause_Patch
	{
		//private bool CanUnpause()
		public static bool Prefix(ref bool __result, Bill_Production __instance)
		{
			if (__instance.repeatMode == RepeatModeDefOf.TD_ColonistCount ||
				__instance.repeatMode == RepeatModeDefOf.TD_XPerColonist)
			{
				__result = __instance.paused && __instance.pauseWhenSatisfied && __instance.recipe.WorkerCounter.CountProducts(__instance) < __instance.TargetCount();
				return false;
			}
			return true;
		}
	}


	[HarmonyPatch(typeof(BillRepeatModeUtility), nameof(BillRepeatModeUtility.MakeConfigFloatMenu))]
	public static class MakeConfigFloatMenu_Patch
	{
		//public static void MakeConfigFloatMenu(Bill_Production bill)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			ConstructorInfo ListCtorInfo = AccessTools.Constructor(typeof(List<>).MakeGenericType(typeof(FloatMenuOption)), new Type[] { });

			foreach (CodeInstruction i in instructions)
			{
				yield return i;
				if (i.opcode == OpCodes.Newobj && i.operand == ListCtorInfo)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);//Bill_Production bill
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MakeConfigFloatMenu_Patch), nameof(InsertMode)));
				}
			}
		}

		public static List<FloatMenuOption> InsertMode(List<FloatMenuOption> options, Bill_Production bill)
		{
			FloatMenuOption item = new FloatMenuOption(RepeatModeDefOf.TD_ColonistCount.LabelCap, delegate
			{
				if (!bill.recipe.WorkerCounter.CanCountProducts(bill))
				{
					Messages.Message("RecipeCannotHaveTargetCount".Translate(), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					bill.repeatMode = RepeatModeDefOf.TD_ColonistCount;
				}
			});
			options.Add(item);

			item = new FloatMenuOption(RepeatModeDefOf.TD_XPerColonist.LabelCap, delegate
			{
				if (!bill.recipe.WorkerCounter.CanCountProducts(bill))
				{
					Messages.Message("RecipeCannotHaveTargetCount".Translate(), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					bill.repeatMode = RepeatModeDefOf.TD_XPerColonist;
				}
			});
			options.Add(item);

			item = new FloatMenuOption(RepeatModeDefOf.TD_WithSurplusIng.LabelCap, delegate
			{
				if (bill.recipe.ingredients.Count() != 1)
				{
					Messages.Message("TD.RecipeCannotHaveSurplus".Translate(), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					bill.repeatMode = RepeatModeDefOf.TD_WithSurplusIng;
				}
			});
			options.Add(item);

			return options;//pass-thru
		}
	}

	[HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
	public static class Dialog_BillConfig_Patch
	{
		//public override void DoWindowContents(Rect inRect)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return OrColonistCount_Transpiler.Transpiler(instructions);
		}
	}
	public static class OrColonistCount_Transpiler
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TranspilerC(instructions, 9999);
		public static IEnumerable<CodeInstruction> TranspilerC(IEnumerable<CodeInstruction> instructions, int count)
		{
			FieldInfo repeatModeInfo = AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.repeatMode));
			FieldInfo TargetCountInfo = AccessTools.Field(typeof(BillRepeatModeDefOf), nameof(BillRepeatModeDefOf.TargetCount));

			int done = 0;
			List <CodeInstruction> instList = instructions.ToList();
			for (int i = 0; i < instList.Count; i++)
			{
				CodeInstruction inst = instList[i];

				//IL_04b4: ldarg.0      // this
				//IL_04b5: ldfld class RimWorld.Bill_Production RimWorld.Dialog_BillConfig::bill
				//IL_04ba: ldfld        class RimWorld.BillRepeatModeDef RimWorld.Bill_Production::repeatMode
				//IL_04bf: ldsfld       class RimWorld.BillRepeatModeDef RimWorld.BillRepeatModeDefOf::TargetCount
				//IL_04c4: bne.un IL_059d

				if (done < count &&
					(inst.opcode == OpCodes.Bne_Un || inst.opcode == OpCodes.Beq) &&  //assembly shows Beq_S but Beq in transpiler
					instList[i - 2].opcode == OpCodes.Ldfld && instList[i - 2].operand == repeatModeInfo &&
					instList[i - 1].opcode == OpCodes.Ldsfld && instList[i - 1].operand == TargetCountInfo)
				{
					done++;
					//Stack is: this.bill.repeatMode, BillRepeatModeDefOf.TargetCount
					//Replacing if(repeatMode == TargetCount) with 
					//(repeatMode == TargetCount || repeatMode == TD_ColonistCount etc)
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OrColonistCount_Transpiler), nameof(ComparePatch)));
					yield return new CodeInstruction(inst.opcode == OpCodes.Bne_Un ? OpCodes.Brfalse : OpCodes.Brtrue, inst.operand);
				}
				else
					yield return inst;
			}
		}

		public static bool ComparePatch(BillRepeatModeDef repeatMode, BillRepeatModeDef targetCountMode)
		{
			return repeatMode == targetCountMode || 
				repeatMode == RepeatModeDefOf.TD_ColonistCount || 
				repeatMode == RepeatModeDefOf.TD_XPerColonist || 
				repeatMode == RepeatModeDefOf.TD_WithSurplusIng;
		}
	}

	[StaticConstructorOnStartup]
	public static class JobDriver_DoBill_Patch
	{
		static JobDriver_DoBill_Patch()
		{
			HarmonyInstance harmony = HarmonyInstance.Create("Uuugggg.rimworld.TD_Enhancement_Pack.main");

			FieldInfo TargetCountInfo = AccessTools.Field(typeof(BillRepeatModeDefOf), nameof(BillRepeatModeDefOf.TargetCount));

			PatchCompilerGenerated.PatchGeneratedMethod(harmony, typeof(Verse.AI.JobDriver_DoBill),
				delegate (MethodInfo method)
				{
					DynamicMethod dm = DynamicTools.CreateDynamicMethod(method, "-unused");

					return (Harmony.ILCopying.MethodBodyReader.GetInstructions(dm.GetILGenerator(), method).
						Any(ilcode => ilcode.operand == TargetCountInfo));
				}, transpiler: new HarmonyMethod(typeof(JobDriver_DoBill_Patch), nameof(Transpiler)));
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return OrColonistCount_Transpiler.Transpiler(instructions);
		}
	}


	//Mod support: Better Workbench Management
	[StaticConstructorOnStartup]
	public static class ImprovedWorkbenches_Patch
	{
		static ImprovedWorkbenches_Patch()
		{
			if(AccessTools.TypeByName("BillConfig_DoWindowContents_Patch") is Type patchType &&
				AccessTools.Method(patchType, "DrawFilters") is MethodInfo patchMethod)
			{
				HarmonyInstance harmony = HarmonyInstance.Create("Uuugggg.rimworld.TD_Enhancement_Pack.main");
				harmony.Patch(patchMethod, transpiler: new HarmonyMethod(typeof(OrColonistCount_Transpiler), "Transpiler"));
			}
		}

	}
}

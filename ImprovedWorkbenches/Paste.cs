﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SupportImprovedWorkbenches
{
	[StaticConstructorOnStartup]
	public static class Paste
	{
		static Paste()
		{
			Log.Message("Supporting BetterWorkbenches");
		}
	}
}
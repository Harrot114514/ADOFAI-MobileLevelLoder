using System;
using System.Collections.Generic;
using UnityEngine;

public static class AsyncInputUtils
{
	public static double GetSongPosition(scrConductor conductor, ulong nowTick)
	{
		if (!GCS.d_oldConductor && !GCS.d_webglConductor)
		{
			return ((double)nowTick / 10000000.0 - conductor.dspTimeSong - (double)scrConductor.calibration_i) * (double)conductor.song.pitch - conductor.addoffset;
		}
		return (double)(conductor.song.time - scrConductor.calibration_i) - conductor.addoffset / (double)conductor.song.pitch;
	}

	public static double GetAngle(scrPlanet planet, double snappedLastAngle, ulong nowTick)
	{
		return snappedLastAngle + (GetSongPosition(planet.conductor, nowTick) - planet.player.lastHit) / planet.conductor.crotchetAtStart * 3.141592653598793 * planet.planetarySystem.speed * (double)(planet.planetarySystem.isCW ? 1 : (-1));
	}

	public static void AdjustAngle(scrPlayer player, ulong targetTick)
	{
		if (AsyncInputManager.isActive && AsyncInputManager.offsetTickUpdated)
		{
			AsyncInputManager.targetSongTick = targetTick - AsyncInputManager.offsetTick;
			player.planetarySystem.chosenPlanet.AsyncRefreshAngles();
		}
	}

	public static void WhileFloorNotChange(scrPlayer player, Action action)
	{
		int num = -1;
		while (num != player.currFloor.seqID)
		{
			num = player.currFloor.seqID;
			action();
		}
	}

	public static void UpdateOffsetTime(long fixDivider = 1L)
	{
		ulong num = AsyncInputManager.currFrameTick - (ulong)(scrConductor.instance.dspTime * 10000000.0);
		long num2 = (long)(num - AsyncInputManager.offsetTick);
		bool flag = true;
		if (fixDivider == 1)
		{
			AsyncInputManager.offsetTick = num;
		}
		else if (Math.Abs(num2) > 0)
		{
			AsyncInputManager.offsetTick += (ulong)(num2 / fixDivider);
		}
		else
		{
			flag = false;
		}
		if (flag)
		{
			AsyncInputManager.offsetTickUpdated = true;
		}
	}

	public static int GetValidKeyCount(IReadOnlyList<KeyCode> keyCodes)
	{
		int num = 0;
		for (int i = 0; i < keyCodes.Count; i++)
		{
			if (++num > 4)
			{
				break;
			}
		}
		return num;
	}
}

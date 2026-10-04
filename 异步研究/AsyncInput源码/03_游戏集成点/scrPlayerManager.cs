using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class scrPlayerManager : ADOBase
{
	public const int MaxPlayers = 4;

	private static scrPlayerManager _instance;

	public static int playerCount = 1;

	public static PlanetColor[] playerColors;

	public static bool[] playerEmoji;

	public static int[] playerOrder;

	public scrPlayer[] allPlayers;

	public scrHitTextManager hitTextManager;

	public scrMistakesManager mistakesManager;

	[NonSerialized]
	public List<scrPlayer> deadPlayersQueue = new List<scrPlayer>();

	public static scrPlayerManager instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = UnityEngine.Object.FindAnyObjectByType<scrPlayerManager>();
			}
			return _instance;
		}
	}

	public scrPlayer[] players { get; private set; }

	public IEnumerator<scrPlayer> GetEnumerator()
	{
		return ((IEnumerable<scrPlayer>)players).GetEnumerator();
	}

	public static void Setup()
	{
		ResetPlayersAppearance();
		SetPlayerCount(1);
	}

	public static void ResetPlayersAppearance()
	{
		playerColors = new PlanetColor[4]
		{
			new PlanetColor(PlanetColorPreset.CoopRed),
			new PlanetColor(PlanetColorPreset.CoopBlue),
			new PlanetColor(PlanetColorPreset.CoopYellow),
			new PlanetColor(PlanetColorPreset.CoopGreen)
		};
		playerEmoji = new bool[4];
	}

	public void ControllerAwake()
	{
		if (ADOBase.isUnityEditor && RDC.force4PlayerCoop)
		{
			SetPlayerCount(4);
		}
		players = new scrPlayer[playerCount];
		for (int i = 0; i < playerCount; i++)
		{
			(players[i] = allPlayers[i]).Init(i);
		}
		deadPlayersQueue.Clear();
		hitTextManager = new scrHitTextManager(this);
		mistakesManager = new scrMistakesManager();
	}

	public static void SetPlayerCount(int playerCount)
	{
		scrPlayerManager.playerCount = playerCount;
		scrMistakesManager.SetPlayerCount(playerCount);
		if (playerCount == 1)
		{
			RDInput.ReassignControllers(1);
		}
		playerOrder = new int[playerCount];
		for (int i = 0; i < playerCount; i++)
		{
			playerOrder[i] = i;
		}
	}

	public bool AnyValidInputWasTriggered()
	{
		scrPlayer[] array = players;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].ValidInputWasTriggered())
			{
				return true;
			}
		}
		return false;
	}

	public List<scrPlayer> GetActivePlayers()
	{
		return players.Where((scrPlayer x) => x.alive && !x.doingRevivalCountdown).ToList();
	}

	public void SetAllPlayerResponsive(bool responsive)
	{
		scrPlayer[] array = players;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].responsive = responsive;
		}
	}

	public void LockAllPlayerInput(float fSecs)
	{
		scrPlayer[] array = players;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].LockInput(fSecs);
		}
	}

	public void UnlockAllPlayerInput()
	{
		scrPlayer[] array = players;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].UnlockInput();
		}
	}

	public void ReunifyPlanets(scrPlayer toPlayer, Vector2? destination = null, bool instant = false)
	{
		scrPlanet chosenPlanet = toPlayer.planetarySystem.chosenPlanet;
		Vector2 position = destination ?? ((Vector2)chosenPlanet.transform.position);
		scrPlayer[] array = players;
		foreach (scrPlayer player in array)
		{
			scrPlanet chosenPlanet2 = player.planetarySystem.chosenPlanet;
			if (player != toPlayer)
			{
				chosenPlanet2.SyncPlanetWithAnother(chosenPlanet);
			}
			if (destination.HasValue || !(player == toPlayer))
			{
				Vector3 vector = new Vector3(position.x, position.y, chosenPlanet2.transform.position.z);
				float duration = (instant ? 0f : 0.3f);
				chosenPlanet2.transform.DOKill();
				if (!chosenPlanet2.transform.position.ApproximatelyXY(vector))
				{
					player.isReunifying = true;
				}
				chosenPlanet2.transform.DOMove(vector, duration).SetEase(Ease.OutCubic).OnComplete(delegate
				{
					player.isReunifying = false;
				});
				scrFloor floorAtPosition = RDUtils.GetFloorAtPosition(position);
				if (floorAtPosition != null)
				{
					chosenPlanet2.currfloor = floorAtPosition;
				}
			}
		}
	}
}

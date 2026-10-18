using System;
using DG.Tweening;

namespace MobileMenu
{
	public class scnMobileMenu : ADOBase
	{
		public static bool firstTimeLoadingScene = true;

		public static IntroPhase introPhase;

		public static bool returnToLevelAfterIntroFinished = false;

		public MobileMenuController menuController;

		public MobileMenuReviewPrompt reviewPromptController;

		public MobileMenuCoopIntro coopIntroController;

		private MobileMenuTitleScreen titleScreen;

		private string mainMap
		{
			get
			{
				if (!GCS.FOOL_JOKER)
				{
					if (!ADOBase.isExpo)
					{
						return "main";
					}
					return "expo";
				}
				return "main_joker";
			}
		}

		private void Awake()
		{
			if (firstTimeLoadingScene)
			{
				introPhase = ((!ADOBase.isSwitch) ? IntroPhase.LoadingServices : IntroPhase.PlayerSelect);
			}
			if (!ADOBase.IsAprilFools())
			{
				GCS.FOOL_JOKER = false;
			}
			scnCLS.DeactivateCustomLevelModifiers();
		}

		private void Start()
		{
			if (introPhase == IntroPhase.PlayerSelected)
			{
				introPhase = ((MobileMenuCoopIntro.GetIntroType() != IntroType.NoIntro) ? IntroPhase.Tutorial : IntroPhase.Finished);
			}
			if (introPhase == IntroPhase.LoadingServices)
			{
				ADOBase.conductor.song2.volume = 0f;
				ADOBase.conductor.song3.volume = 0f;
				menuController.LoadMapAsync(mainMap);
				titleScreen = menuController.map.rootGroup[0] as MobileMenuTitleScreen;
				titleScreen.title.SetLoading(loading: true);
				titleScreen.title.foreground.SetActive(value: true);
				menuController.JumpToScreen(titleScreen, instant: true);
				menuController.ShowButtons(show: false, instant: true);
				menuController.enabled = false;
				MobileMenuController mobileMenuController = menuController;
				mobileMenuController.onFinishLoading = (Action)Delegate.Combine(mobileMenuController.onFinishLoading, (Action)delegate
				{
					titleScreen.title.SetLoading(loading: false);
					menuController.ShowButtons(show: true);
					menuController.enabled = true;
					titleScreen.title.foreground.SetActive(value: false);
					if (Persistence.passedMobileMenuTutorial)
					{
						ShowIntroScreen();
					}
					else
					{
						coopIntroController.Run();
					}
				});
			}
			else if (introPhase == IntroPhase.Tutorial)
			{
				ADOBase.conductor.song2.volume = 0f;
				ADOBase.conductor.song3.volume = 0f;
				menuController.LoadMap(mainMap);
				menuController.JumpToScreen(titleScreen, instant: true);
				menuController.ShowButtons(show: false, instant: true);
				menuController.enabled = false;
				coopIntroController.Run();
				ADOBase.playerManager.SetAllPlayerResponsive(responsive: true);
				firstTimeLoadingScene = false;
			}
			else if (introPhase == IntroPhase.PlayerSelect)
			{
				ADOBase.conductor.song.volume = 0f;
				ADOBase.conductor.song2.volume = 0f;
				ADOBase.conductor.song3.volume = 0f;
				ADOBase.controller.paused = true;
				ADOBase.controller.pauseMenu.Show(PauseMenu.Submenu.PlayerSelect);
				firstTimeLoadingScene = false;
			}
			else
			{
				scrPlayer[] players = scrPlayerManager.instance.players;
				for (int num = 0; num < players.Length; num++)
				{
					players[num].gameObject.SetActive(value: false);
				}
				menuController.LoadMap(mainMap);
				ShowIntroScreen();
			}
		}

		private void ShowIntroScreen()
		{
			if (!reviewPromptController.TryRunReviewPrompt())
			{
				ADOBase.conductor.song2.DOFade(1f, 0.5f);
				ADOBase.conductor.song3.DOFade(1f, 0.5f);
				menuController.JumpToMenuEntrance();
			}
			GameServices instance = GameServices.Instance;
			if (instance != null && instance.showRetroactiveAchievements)
			{
				instance.showRetroactiveAchievements = false;
				if (instance.achievementsQueue.Count > 0)
				{
					scrUIController.instance.ShowRetroactiveAchievements();
				}
			}
			if (!ADOBase.controller.paused)
			{
				scrUIController.instance.pauseButton.gameObject.SetActive(!ADOBase.isDesktop);
			}
			ADOBase.playerManager.allPlayers[0].planetarySystem.gameObject.SetActive(value: false);
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: true);
			firstTimeLoadingScene = false;
		}
	}
}

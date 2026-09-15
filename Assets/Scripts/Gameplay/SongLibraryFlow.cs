using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdiotTape.Gameplay
{

    public sealed class SongLibraryFlow : MonoBehaviour
    {

        [SerializeField] private SongCatalog catalog;
        [SerializeField] private GameObject previewAudioRoot;
        private SongLibraryView view;
        private SongPreviewPlayer preview;
        private GameplaySession session;
        private PlayRequest request;
        private bool busy;
        private bool cancelled;
        private string failure;
        private Scene gameplayScene;
        public bool IsBusy => busy;
        public GameplaySession Session => session;

        private void Start()
        {

            preview = gameObject.AddComponent<SongPreviewPlayer>();
            view = gameObject.AddComponent<SongLibraryView>();
            view.Build(catalog, preview, StartPlay, CancelPreparation);

        }

        public void StartPlay(PlayRequest selected)
        {

            if (busy || session != null || selected == null)
            {

                return;

            }

            if (!Application.CanStreamedLevelBeLoaded("Gameplay"))
            {

                view.SetLoading(false, "Gameplay 씬을 불러올 수 없습니다.");
                return;

            }
            request = selected;
            cancelled = false;
            failure = null;
            StartCoroutine(LoadGameplay());

        }

        private IEnumerator LoadGameplay()
        {

            busy = true;
            preview.StopPreview();
            view.SetLoading(true, "플레이를 준비하고 있습니다…");
            SceneManager.sceneLoaded += ConfigureLoadedSession;
            AsyncOperation loading = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Additive);
            yield return loading;
            SceneManager.sceneLoaded -= ConfigureLoadedSession;
            if (session == null && string.IsNullOrEmpty(failure))
            {

                failure = "게임 씬에서 세션을 찾지 못했습니다.";

            }
            while (!cancelled && failure == null && session != null && !session.HasStartedAttempt)
            {

                yield return null;

            }

            if (cancelled || failure != null)
            {

                yield return UnloadGameplay();
                view.SetLoading(false, failure ?? "플레이 준비를 취소했습니다.");
                view.ResumePreview();

            }
            else
            {

                view.SetVisible(false);

            }

            busy = false;

        }

        private void ConfigureLoadedSession(Scene scene, LoadSceneMode mode)
        {

            if (scene.name != "Gameplay")
            {

                return;

            }
            gameplayScene = scene;
            if (previewAudioRoot != null)
            {

                previewAudioRoot.SetActive(false);

            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {

                session = root.GetComponentInChildren<GameplaySession>();
                if (session != null)
                {

                    break;

                }

            }

            if (session == null)
            {

                return;

            }
            session.ConfigureRequest(request);
            session.ReturnToLibraryRequested += ReturnToLibrary;
            session.PreparationFailed += HandleFailure;

        }

        private void HandleFailure(string error) => failure = error;
        public void CancelPreparation() => cancelled = true;

        private void ReturnToLibrary()
        {

            if (busy)
            {

                cancelled = true;
                return;

            }

            StartCoroutine(Return());

        }

        private IEnumerator Return()
        {

            busy = true;
            view.SetVisible(true);
            view.SetLoading(true, "곡 목록으로 돌아갑니다…");
            yield return UnloadGameplay();
            view.SetLoading(false, failure ?? string.Empty);
            view.ResumePreview();
            busy = false;

        }

        private IEnumerator UnloadGameplay()
        {

            if (previewAudioRoot != null)
            {

                previewAudioRoot.SetActive(true);

            }

            if (session != null)
            {

                session.ReturnToLibraryRequested -= ReturnToLibrary;
                session.PreparationFailed -= HandleFailure;

            }

            if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            {

                yield return SceneManager.UnloadSceneAsync(gameplayScene);

            }

            session = null;
            gameplayScene = default;

        }

        private void OnDestroy()
        {

            SceneManager.sceneLoaded -= ConfigureLoadedSession;
            if (session != null)
            {

                session.ReturnToLibraryRequested -= ReturnToLibrary;
                session.PreparationFailed -= HandleFailure;

            }

        }

    }

}

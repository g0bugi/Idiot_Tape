using UnityEngine;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    public sealed class GameplayHud : MonoBehaviour
    {

        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text judgementText;
        [SerializeField] private Image progressFill;

        public void SetScore(int score, int combo)
        {

            scoreText.text = score.ToString("00000000");
            comboText.text = combo > 0 ? $"COMBO  {combo:000}" : string.Empty;

        }

        public void SetProgress(float normalizedProgress)
        {

            progressFill.fillAmount = Mathf.Clamp01(normalizedProgress);

        }

        public void ShowJudgement(JudgementGrade grade)
        {

            judgementText.text = grade == JudgementGrade.None ? string.Empty : grade.ToString().ToUpperInvariant();

        }

        public void SetPaused(bool paused)
        {

            judgementText.text = paused ? "PAUSED" : string.Empty;

        }

    }

}

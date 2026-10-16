using UnityEngine;
using TMPro;
namespace OptimizationLab
{
    public sealed class LabHud : MonoBehaviour
    {
        public TextMeshProUGUI timerText, levelText, killsText, healthText, sceneText;
        public TextMeshProUGUI endTitle, endDetails, diagnosticText, modeText, novaText;
        public UnityEngine.UI.Image healthFill, xpFill, damageFlash;
        public GameObject endPanel, diagnosticPanel;
        float refreshAt;
        public void Draw(SurvivorGame game)
        {
            healthFill.fillAmount = game.Health / 100f;
            xpFill.fillAmount = (float)game.Experience / game.NextLevelExperience;
            Color flash = damageFlash.color;
            flash.a = Mathf.Max(0, flash.a - Time.unscaledDeltaTime * 1.5f);
            damageFlash.color = flash;
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.1f;
            timerText.SetText("{0:00}:{1:00}", Mathf.Floor(game.Elapsed / 60), Mathf.Floor(game.Elapsed % 60));
            healthText.SetText("{0:0} / 100", game.Health);
            levelText.SetText("LV. {0}", game.Level);
            killsText.SetText("{0}  SLAIN", game.Kills);
            sceneText.text = game.SceneLabel;
            modeText.text = game.AutoDemo ? "F2  AUTO PILOT ON" : "F2  AUTO PILOT OFF";
            novaText.text = game.NovaCooldown <= 0 ? "SPACE   SOUL NOVA  /  READY" : "SOUL NOVA  /  RECHARGING";
            if (diagnosticPanel.activeSelf)
                diagnosticText.SetText("FRAME {0:1} ms  /  {1:0} FPS\nENEMIES {2}   EXPERIMENT {3:1} ms",
                    game.SmoothedFrameMs, 1000f / Mathf.Max(0.1f, game.SmoothedFrameMs),
                    game.ActiveEnemies, (float)game.ExperimentWorkMs);
            endPanel.SetActive(game.Finished);
            if (game.Finished)
            {
                endTitle.text = game.Victory ? "DAWN BREAKS" : "THE NIGHT CLAIMS YOU";
                endDetails.SetText("{0} SLAIN    /    LEVEL {1}\nPRESS R TO RETURN", game.Kills, game.Level);
            }
        }
        public void FlashDamage() { damageFlash.color = new Color(0.8f, 0.12f, 0.18f, 0.2f); }
        public void ToggleDiagnostics() { diagnosticPanel.SetActive(!diagnosticPanel.activeSelf); }
    }
}

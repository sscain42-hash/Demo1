using UnityEngine;

namespace Combat.Parry
{
    /// <summary>
    /// Hiển thị state + window frame lên màn hình để debug.
    /// Gắn vào cùng GameObject với ParryController.
    /// </summary>
    public class ParryDebugger : MonoBehaviour
    {
        [SerializeField] private ParryController parryController;
        [SerializeField] private bool showDebugger = true;

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!showDebugger) return;

            if (parryController == null)
                parryController = GetComponent<ParryController>();

            if (parryController == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                _style.normal.textColor = Color.cyan;
            }

            string info = $"<b>PARRY STATE:</b> {parryController.CurrentState}\n" +
                          $"<b>Window Frames:</b> {parryController.CurrentWindowFrames}\n" +
                     
                          $"<b>Chain Count:</b> {parryController.CurrentChainCount}\n" +
                          $"<b>Telegraphing:</b> {parryController.IsTelegraphing}";

            GUI.Box(new Rect(10, 110, 320, 130), info, _style);
        }
    }
}
using TMPro;
using UnityEngine;

namespace Game
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;

        public void SetMessage(string text)
        {
            if (_messageText == null) return;
            _messageText.text = text;
            _messageText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// View thuần: chỉ vẽ và phát tín hiệu bấm nút. Không biết mạng là gì.
    /// </summary>
    public sealed class LoginUi : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _registerButton;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private GameObject _root;

        public string Username => _usernameInput.text;
        public string Password => _passwordInput.text;

        public Button LoginButton => _loginButton;
        public Button RegisterButton => _registerButton;

        private void Awake()
        {
            // Ô mật khẩu phải là Password content type — kiểm bằng code vì rất dễ quên set trong Inspector,
            // và quên thì mật khẩu hiện nguyên trên màn hình lúc quay video demo.
            _passwordInput.contentType = TMP_InputField.ContentType.Password;
            _passwordInput.ForceLabelUpdate();
        }

        public void ShowMessage(string text, bool isError)
        {
            _messageText.text = text;
            _messageText.color = isError ? new Color(0.9f, 0.3f, 0.3f) : Color.white;
        }

        public void SetInteractable(bool value)
        {
            _loginButton.interactable = value;
            _registerButton.interactable = value;
        }

        public void SetVisible(bool value)
        {
            _root.SetActive(value);
        }
    }
}
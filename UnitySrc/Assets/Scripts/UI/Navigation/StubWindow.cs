using TMPro;
using UnityEngine;

namespace UI.Navigation
{
    public class StubWindow : AppWindow
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        public void Setup(string title, string body)
        {
            if (_titleLabel != null)
                _titleLabel.text = title;

            if (_bodyLabel != null)
            {
                _bodyLabel.text = body;
                _bodyLabel.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }
        }
    }
}

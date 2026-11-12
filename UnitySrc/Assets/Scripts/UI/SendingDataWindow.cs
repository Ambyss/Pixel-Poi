using UnityEngine;

namespace UI
{
    public class SendingDataWindow : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
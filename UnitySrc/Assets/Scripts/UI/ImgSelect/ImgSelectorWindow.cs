using BLE;
using DefaultNamespace.Misc;
using UnityEngine;
using Zenject;

namespace UI
{
    public class ImgSelectorWindow : MonoBehaviour
    {
        [SerializeField] private ImgSelectCell _cellPrefab;
        
        [Inject] private ImgSender _sender;
        [Inject] private SavesController _savesController;
    }
}
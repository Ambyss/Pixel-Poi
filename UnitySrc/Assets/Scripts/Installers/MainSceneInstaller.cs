using Android.BLE;
using BLE;
using DefaultNamespace.Misc;
using Zenject;

namespace DefaultNamespace
{
    public class MainSceneInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<FlexibleColorPicker>().FromComponentInHierarchy().AsSingle();
            Container.Bind<BleManager>().FromComponentInHierarchy().AsSingle().NonLazy();
            Container.Bind<AckReceiver>().AsSingle().NonLazy();
            Container.Bind<ImgSender>().AsSingle().NonLazy();
            Container.Bind<BLESender>().AsSingle().NonLazy();
            Container.Bind<BLEController>().AsSingle().NonLazy();
            Container.Bind<SavesController>().AsSingle().NonLazy();
        }
    }
}

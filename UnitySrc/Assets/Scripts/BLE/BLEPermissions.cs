using UnityEngine;
using UnityEngine.Android;

public class BLEPermissions : MonoBehaviour
{
    void Start()
    {
        Input.location.Start();
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
    }
}
